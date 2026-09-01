using Orbit.Contracts.Calendar;
using Orbit.Contracts.Tasks;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Location;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>How far an entry's appointment got - what the screen has to tell the reader afterwards.</summary>
public enum AppointmentOutcome
{
    /// <summary>On the server, named, and visible to everybody the calendar is shared with.</summary>
    Reached,

    /// <summary>Written to this phone's own calendar and waiting to be named - see PendingCalendarLink.</summary>
    QueuedOnThisPhone,

    /// <summary>Refused: somebody else can change it and there is no connection to check with.</summary>
    Refused
}

/// <param name="Entry">
/// The entry carrying whatever appointment id it should, or null when even the local write was refused -
/// so the caller stops rather than saving an entry pointing at an appointment nobody made.
/// </param>
/// <param name="PlaceWasNotSaved">
/// True when somebody said where it happens and that could not be kept - a name nothing could be found
/// for, or no connection to look it up with. The appointment is still saved; the place is what is
/// missing, and saying so beats a box that quietly empties itself on the next open.
/// </param>
public sealed record AppointmentResult(
    TaskItemDto? Entry, AppointmentOutcome Outcome, bool PlaceWasNotSaved = false);

/// <summary>
/// The appointment a Calendar entry carries: brought into being, or into step, whenever the entry is
/// saved.
///
/// Its own object rather than three more fields on the task list screen. A calendar client, the local
/// calendar and the phone's belief about connectivity only ever travel together and only ever serve this
/// one question - which is what the project's grouping convention is about.
/// </summary>
public sealed class EntryAppointment
{
    private readonly LocalCalendarEventRepository _events;
    private readonly CalendarClient _calendarClient;
    private readonly INetworkStatus _networkStatus;

    /// <summary>
    /// Where a place somebody typed actually is. An appointment stores a point first, and the box on the
    /// entry takes words - so a name with no pin behind it is looked up here rather than dropped.
    /// </summary>
    private readonly PlaceSearch _places;

    public EntryAppointment(
        LocalCalendarEventRepository events, CalendarClient calendarClient, INetworkStatus networkStatus,
        PlaceSearch places)
    {
        _events = events;
        _calendarClient = calendarClient;
        _networkStatus = networkStatus;
        _places = places;
    }

    /// <summary>
    /// Every appointment this phone holds that the server has named, by that name - what lets an entry
    /// show when it happens without asking anybody. Read from the local calendar, so it is there with no
    /// connection like everything else on the screen above.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, CalendarEventDetailsDto>> KnownByServerIdAsync(
        CancellationToken cancellationToken)
        => (await _events.GetAllAsync(cancellationToken))
            .Where(candidate => candidate.ServerId is not null)
            .ToDictionary(candidate => candidate.ServerId!.Value, candidate => candidate.Details);

    /// <summary>
    /// The appointment an entry made offline is waiting on, if any. Keyed on the words rather than an id
    /// for the reason PendingCalendarLink gives: an entry made offline has no id of its own yet.
    /// </summary>
    public Task<LocalCalendarEvent?> FindWaitingForAsync(
        Guid taskListLocalId, string description, CancellationToken cancellationToken)
        => _events.FindPendingForAsync(taskListLocalId, description, cancellationToken);

    /// <summary>
    /// Brings the appointment into being, or into step, and hands back the entry carrying whatever id it
    /// should. Called before the list is written rather than after, so there is no window where the
    /// entry exists and the appointment does not - the order Orbit.Web's SaveTheCalendarAsync settles on.
    ///
    /// Online this goes straight to the server, which names the event and lets the entry carry that name
    /// immediately. Offline it writes the event to this phone's own calendar and remembers the pairing
    /// (see PendingCalendarLink): the entry carries no server id yet, and gets one when the calendar
    /// syncs. Both are real appointments - the difference is only whether anybody else can see one yet,
    /// which is what the row's tag says.
    /// </summary>
    public async Task<AppointmentResult> SaveAsync(
        TaskItemEditor editor, TaskItemDto edited, Guid taskListLocalId, CancellationToken cancellationToken)
    {
        var place = await WhereItHappensAsync(editor, cancellationToken);

        // Asked before trying rather than learned from the attempt. With no route a request does not
        // fail quickly and cleanly - it hangs until the client gives up, which arrives as a timeout
        // rather than an HttpRequestException, and a catch written for the latter let an appointment
        // saved on a phone with no connection call itself "online". Found on a device.
        if (!_networkStatus.IsOnline)
        {
            return await OnThisPhoneAsync(editor, edited, taskListLocalId, place, cancellationToken);
        }

        var details = editor.Event.ToRequest(edited.Description, place);
        try
        {
            if (edited.LinkedCalendarEventId is { } eventId)
            {
                await _calendarClient.UpdateAsync(eventId, new UpdateCalendarEventRequest(details), cancellationToken);
                return new AppointmentResult(edited, AppointmentOutcome.Reached, LostThePlace(place));
            }

            return new AppointmentResult(
                edited with
                {
                    LinkedCalendarEventId = await _calendarClient.CreateAsync(
                        new CreateCalendarEventRequest(details), cancellationToken)
                },
                AppointmentOutcome.Reached,
                LostThePlace(place));
        }
        // Both, because a connection that is up but going nowhere ends either way - and the fallback
        // is the same whichever it was.
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return await OnThisPhoneAsync(editor, edited, taskListLocalId, place, cancellationToken);
        }
    }

    /// <summary>
    /// Where the entry says it happens, in the shape an appointment can store.
    ///
    /// A pin already carries its point. A name somebody typed does not, so it is looked up - the same
    /// service the picker uses in reverse. What cannot be found is left as nowhere rather than guessed
    /// at: an appointment pinned to the wrong town is worse than one that says only when it is.
    /// </summary>
    private async Task<EventPlace> WhereItHappensAsync(TaskItemEditor editor, CancellationToken cancellationToken)
    {
        var typed = editor.Location.Trim();
        if (typed.Length == 0)
        {
            return EventPlace.Nowhere;
        }

        if (editor.LocationLatitude is { } latitude && editor.LocationLongitude is { } longitude)
        {
            return new EventPlace(typed, latitude, longitude);
        }

        try
        {
            return await _places.SearchAsync(typed, limit: 1, cancellationToken) is [var found, ..]
                ? new EventPlace(typed, found.Latitude, found.Longitude)
                : new EventPlace(typed);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // Nowhere to look it up from. The name is kept on the result so the caller can say the place
            // could not be saved, rather than saving an appointment that quietly lost it.
            return new EventPlace(typed);
        }
    }

    /// <summary>
    /// The offline half: the appointment is written here and waits to be named. An entry already being
    /// corrected keeps the event it made earlier rather than making a second one - which is the whole
    /// reason the pairing is remembered rather than inferred.
    /// </summary>
    private async Task<AppointmentResult> OnThisPhoneAsync(
        TaskItemEditor editor, TaskItemDto edited, Guid taskListLocalId, EventPlace place,
        CancellationToken cancellationToken)
    {
        var details = editor.Event.ToDetails(edited.Description, place);
        if (await _events.FindPendingForAsync(taskListLocalId, edited.Description, cancellationToken) is { } waiting)
        {
            return await _events.UpdateAsync(waiting.LocalId, details, cancellationToken) is LocalWriteOutcome.RefusedWhileOffline
                ? new AppointmentResult(null, AppointmentOutcome.Refused)
                : new AppointmentResult(edited, AppointmentOutcome.QueuedOnThisPhone, LostThePlace(place));
        }

        var created = await _events.CreateAsync(details, cancellationToken);
        await _events.RememberPendingLinkAsync(
            created.LocalId, taskListLocalId, edited.Description, cancellationToken);

        return new AppointmentResult(edited, AppointmentOutcome.QueuedOnThisPhone, LostThePlace(place));
    }

    /// <summary>Somebody said where, and nothing could be found for it - see AppointmentResult.</summary>
    private static bool LostThePlace(EventPlace place) => place.Name.Length > 0 && !place.CanBeSaved;
}
