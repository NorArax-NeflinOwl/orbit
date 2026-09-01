using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Calendar;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Google;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Location;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Screens.Sharing;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Calendar;

/// <summary>
/// One calendar event. The counterpart of the note and task-list editors, and shaped the same way: the
/// change is written to the local database first and queued from there.
///
/// Deliberately narrower than Orbit.Web's editor. Guests, recurrence, colour and per-event notification
/// channels are all real fields (see <see cref="CalendarEventDetailsDto"/>) and all carried through
/// untouched here rather than dropped - a phone that saved a partial event would silently strip
/// somebody's recurrence rule the first time they fixed a typo in the title.
/// </summary>
public sealed partial class CalendarEventDetailViewModel : ObservableObject
{
    private readonly LocalCalendarEventRepository _events;

    /// <summary>Only to say why this is read-only, in the same words the calendar's own rows use.</summary>
    private readonly INetworkStatus _networkStatus;

    /// <summary>
    /// Where a place somebody typed actually is. An event stores a point first, so a name alone cannot
    /// be saved - see TryFindTheTypedPlaceAsync.
    /// </summary>
    private readonly Orbit.Mobile.Location.PlaceSearch _places;
    private readonly CalendarEventSynchronizer _synchronizer;
    private readonly CalendarClient _calendarClient;
    private readonly EditLock _editLock;
    private readonly GoogleIntegrationAccess _google;
    private readonly IDeviceLocation _deviceLocation;
    private readonly ChatRepository _contacts;
    private readonly Translations _translations;
    private readonly IScreenNavigator _navigator;

    private Guid _localId;

    /// <summary>
    /// Everything this screen does not show. Kept whole and sent back unchanged, which is what stops an
    /// edit here from being a quiet deletion of what the browser set.
    /// </summary>
    private CalendarEventDetailsDto? _loaded;

    /// <summary>
    /// How much this event matters. Orbit.Web's event editor has had the same three choices all along -
    /// it is what sorts an event against the others and what the dashboard's filter reads - and the
    /// phone could neither set one nor keep the one a browser had set.
    /// </summary>
    public IReadOnlyList<Tasks.PriorityChoice> Priorities { get; }

    [ObservableProperty]
    private Tasks.PriorityChoice _chosenPriority;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private DateTime _startDate = DateTime.Today;

    [ObservableProperty]
    private TimeSpan _startTime;

    /// <summary>
    /// The day it ends on, which is not always the day it starts. The end used to be built from
    /// StartDate, so an event spanning days was quietly shortened to one the first time anybody fixed a
    /// typo in its title.
    /// </summary>
    [ObservableProperty]
    private DateTime _endDate = DateTime.Today;

    [ObservableProperty]
    private TimeSpan _endTime;

    /// <summary>
    /// What the place is called. A location is a point with an optional label - see EventLocationDto -
    /// so an address on its own is not one, and the coordinates come from the phone rather than from a
    /// map the way Orbit.Web picks them.
    /// </summary>
    [ObservableProperty]
    private string _locationAddress = string.Empty;

    private double? _locationLatitude;
    private double? _locationLongitude;

    public bool HasLocation => _locationLatitude is not null;

    /// <summary>
    /// The event's place in Google Maps, and the way there. The directions link deliberately carries no
    /// origin: Google routes from wherever the reader is when they open it, which on a phone standing
    /// somewhere is the whole point - see GoogleMapsLink.
    /// </summary>
    public string? LocationInGoogleMapsUrl
        => _locationLatitude is { } latitude && _locationLongitude is { } longitude
            ? GoogleMapsLink.ToPlace(latitude, longitude)
            : null;

    public string? LocationDirectionsUrl
        => _locationLatitude is { } latitude && _locationLongitude is { } longitude
            ? GoogleMapsLink.ToDirections(latitude, longitude)
            : null;

    /// <summary>A place to point at, and an account allowed to point at it - see GoogleIntegrationAccess.</summary>
    public bool CanOpenLocationInGoogleMaps => HasGoogleExtras && HasLocation;

    [ObservableProperty]
    private bool _isAllDay;

    /// <summary>
    /// Whether it repeats, and how. The three frequencies are Orbit.Core's own - see RecurrenceDto -
    /// and the phone offers exactly them rather than a shorter list of its own.
    /// </summary>
    [ObservableProperty]
    private bool _isRecurring;

    [ObservableProperty]
    private string _recurrenceFrequency = "Weekly";

    /// <summary>Every how many of that frequency. One is every week; two is every other week.</summary>
    [ObservableProperty]
    private int _recurrenceIntervalCount = 1;

    /// <summary>Whether it stops, and when. Off means it repeats without an end, as the web's blank does.</summary>
    [ObservableProperty]
    private bool _recurrenceEnds;

    [ObservableProperty]
    private DateTime _recurrenceUntil = DateTime.Today;

    /// <summary>The frequencies, for the picker - in Orbit.Core's own order.</summary>
    public IReadOnlyList<RecurrenceChoice> Frequencies { get; private set; } = [];

    /// <summary>The palette, with the chosen one marked - see EventColourChoice.</summary>
    public ObservableCollection<EventColourChoice> Colours { get; } = [];

    private string? _colour;

    /// <summary>Who is invited, by name. Orbit.Web offers the same list, from the same contacts.</summary>
    public ObservableCollection<GuestRow> Guests { get; } = [];

    /// <summary>Everyone who could be invited and is not already - the contacts this phone knows.</summary>
    public ObservableCollection<GuestRow> ContactsToInvite { get; } = [];

    public bool HasNobodyToInvite => ContactsToInvite.Count == 0;

    /// <summary>The reminders set on this event, each as the sentence it will read as.</summary>
    public ObservableCollection<ReminderRow> Reminders { get; } = [];

    /// <summary>What can be added, which is Orbit.Web's own eleven - see ReminderChoice.</summary>
    public IReadOnlyList<ReminderChoice> ReminderChoices { get; private set; } = [];

    /// <summary>Picking one adds it; there is no separate "add" to press afterwards.</summary>
    [ObservableProperty]
    private ReminderChoice? _reminderToAdd;

    /// <summary>How it is announced as it approaches. The same choices Orbit.Web offers.</summary>
    public IReadOnlyList<NotificationChannelChoice> Channels { get; private set; } = [];

    [ObservableProperty]
    private NotificationChannelChoice? _reminderChannel;

    /// <summary>Bound to the picker, which needs an object out of Frequencies rather than a string.</summary>
    public RecurrenceChoice? ChosenFrequency
    {
        get => Frequencies.FirstOrDefault(choice => choice.Value == RecurrenceFrequency);
        set
        {
            if (value is not null)
            {
                RecurrenceFrequency = value.Value;
            }
        }
    }

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isReadOnly;

    /// <summary>
    /// Whether this account may hand the event to Google - see GoogleIntegrationAccess for who
    /// qualifies. Read when the screen loads, so the offer does not flicker in after the form.
    /// </summary>
    [ObservableProperty]
    private bool _hasGoogleExtras;

    public CalendarEventDetailViewModel(
        LocalCalendarEventRepository events, CalendarEventSynchronizer synchronizer, Translations translations,
        SharePanel share, IScreenNavigator navigator,
        CalendarClient calendarClient, EditLock editLock, IDeviceLocation deviceLocation,
        ChatRepository contacts, GoogleIntegrationAccess google, INetworkStatus networkStatus,
        Orbit.Mobile.Location.PlaceSearch places)
    {
        _places = places;
        _networkStatus = networkStatus;
        _events = events;
        _synchronizer = synchronizer;
        _translations = translations;
        Share = share;
        _navigator = navigator;
        _calendarClient = calendarClient;
        _editLock = editLock;
        _google = google;
        _deviceLocation = deviceLocation;
        _contacts = contacts;
        Frequencies = RecurrenceChoice.All(translations);
        Priorities = Tasks.PriorityChoice.All(translations);
        _chosenPriority = Tasks.PriorityChoice.For(
            nameof(Orbit.Core.Abstractions.ItemPriority.Normal), translations);
        ReminderChoices = ReminderChoice.All(translations);
        Channels = NotificationChannelChoice.All(translations);
        _editLock.Changed += (_, _) => ShowWhoElseIsEditing();
    }

    /// <summary>Offering this to somebody else - see SharePanel.</summary>
    public SharePanel Share { get; }

    public bool HasStatus => Status.Length > 0;

    public bool CanEdit => !IsReadOnly;

    /// <summary>False until a load has succeeded: saving what was never read would write guesses.</summary>
    public bool CanSave => _loaded is not null && CanEdit && Title.Trim().Length > 0;

    /// <summary>
    /// What the pickers add up to. The screen edits local dates and times, and two things need the same
    /// two instants - the event that gets saved and the Google link below - so they are worked out here
    /// rather than twice.
    /// </summary>
    private DateTimeOffset ChosenStartUtc => ToUtc(StartDate.Date + (IsAllDay ? TimeSpan.Zero : StartTime));

    private DateTimeOffset ChosenEndUtc
        => ToUtc(IsAllDay ? EndDate.Date + TimeSpan.FromDays(1) : EndDate.Date + EndTime);

    private static DateTimeOffset ToUtc(DateTime local)
        => new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)).ToUniversalTime();

    /// <summary>
    /// The event as a Google Calendar "add this" link, built from what is on screen rather than from
    /// what was last saved - so a change made and not yet saved is what gets handed over, which is what
    /// somebody tapping it while editing means.
    /// </summary>
    public string AddToGoogleCalendarUrl
        => GoogleCalendarEventLink.ForEvent(
            Title.Trim(), ChosenStartUtc, ChosenEndUtc, IsAllDay,
            Description.Trim() is { Length: > 0 } description ? description : null,
            LocationAddress.Trim() is { Length: > 0 } address ? address : null,
            RecurrenceOrNothing());

    /// <summary>An event with no title is not worth handing over, and an account that does not qualify may not.</summary>
    public bool CanAddToGoogleCalendar => HasGoogleExtras && Title.Trim().Length > 0;

    public void Open(Guid localId) => _localId = localId;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        HasGoogleExtras = await _google.IsAvailableAsync(cancellationToken);
        await ShowStoredEventAsync(cancellationToken);
    }

    /// <summary>
    /// Where the phone is, as the place. Orbit.Web picks a point off a map; a phone knows where it is,
    /// which is the same answer arrived at more directly.
    /// </summary>
    [RelayCommand]
    private async Task UseMyLocationAsync(CancellationToken cancellationToken)
    {
        var here = await _deviceLocation.ReadAsync(cancellationToken);
        if (here.Outcome is not DeviceLocationOutcome.Found)
        {
            // The two refusals read the same to somebody standing here: no place was recorded.
            Status = _translations["Couldn't work out where this phone is."];
            return;
        }

        _locationLatitude = here.Latitude;
        _locationLongitude = here.Longitude;
        // Reverse geocoding often comes back empty, and a name already typed is worth more than none.
        if (LocationAddress.Trim().Length == 0 && here.Address is { Length: > 0 } address)
        {
            LocationAddress = address;
        }

        OnPropertyChanged(nameof(HasLocation));
        OnPropertyChanged(nameof(LocationInGoogleMapsUrl));
        OnPropertyChanged(nameof(LocationDirectionsUrl));
        OnPropertyChanged(nameof(CanOpenLocationInGoogleMaps));
        await SaveAsync(cancellationToken);
    }

    [RelayCommand]
    private Task RemoveLocationAsync(CancellationToken cancellationToken)
    {
        _locationLatitude = null;
        _locationLongitude = null;
        LocationAddress = string.Empty;
        OnPropertyChanged(nameof(HasLocation));
        OnPropertyChanged(nameof(LocationInGoogleMapsUrl));
        OnPropertyChanged(nameof(LocationDirectionsUrl));
        OnPropertyChanged(nameof(CanOpenLocationInGoogleMaps));
        return SaveAsync(cancellationToken);
    }

    /// <summary>
    /// A location is a point with an optional label, so an address typed with no point behind it is not
    /// one and is not sent - the same rule Orbit.Web's editor applies.
    /// </summary>
    /// <summary>
    /// Adding the same reminder twice would be two of the same sentence at the same moment, so a
    /// duplicate is dropped rather than refused - the reader asked for it, and it is already there.
    /// </summary>
    partial void OnReminderToAddChanged(ReminderChoice? value)
    {
        if (value is null)
        {
            return;
        }

        if (Reminders.All(reminder => reminder.MinutesBefore != value.MinutesBefore))
        {
            var added = new ReminderRow(value.MinutesBefore, value.Name);
            var position = Reminders.Count(reminder => reminder.MinutesBefore < value.MinutesBefore);
            Reminders.Insert(position, added);
            SaveCommand.Execute(null);
        }

        ReminderToAdd = null;
    }

    [RelayCommand]
    private Task ChooseColourAsync(EventColourChoice? colour, CancellationToken cancellationToken)
    {
        if (colour is null)
        {
            return Task.CompletedTask;
        }

        _colour = colour.Value;
        ShowColours();
        return SaveAsync(cancellationToken);
    }

    private void ShowColours()
    {
        Colours.Clear();
        foreach (var colour in EventColourChoice.All(_colour, _translations))
        {
            Colours.Add(colour);
        }
    }

    /// <summary>Everyone this phone knows who is not already coming.</summary>
    private void ShowWhoCouldBeInvited(IReadOnlyList<Data.LocalContact> contacts)
    {
        ContactsToInvite.Clear();
        foreach (var contact in contacts.Where(contact => Guests.All(guest => guest.UserId != contact.UserId)))
        {
            ContactsToInvite.Add(new GuestRow(contact.UserId, contact.DisplayName));
        }

        OnPropertyChanged(nameof(HasNobodyToInvite));
    }

    [RelayCommand]
    private async Task InviteAsync(GuestRow? guest, CancellationToken cancellationToken)
    {
        if (guest is null || Guests.Any(invited => invited.UserId == guest.UserId))
        {
            return;
        }

        Guests.Add(guest);
        ShowWhoCouldBeInvited(await _contacts.GetContactsAsync(cancellationToken));
        await SaveAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task UninviteAsync(GuestRow? guest, CancellationToken cancellationToken)
    {
        if (guest is null || !Guests.Remove(guest))
        {
            return;
        }

        ShowWhoCouldBeInvited(await _contacts.GetContactsAsync(cancellationToken));
        await SaveAsync(cancellationToken);
    }

    [RelayCommand]
    private Task RemoveReminderAsync(ReminderRow? reminder, CancellationToken cancellationToken)
    {
        if (reminder is null || !Reminders.Remove(reminder))
        {
            return Task.CompletedTask;
        }

        return SaveAsync(cancellationToken);
    }

    /// <summary>
    /// The rule, or none. "Until" is sent as the end of that day so a rule that repeats until the 20th
    /// includes the 20th, which is what somebody picking that date means.
    /// </summary>
    private RecurrenceDto? RecurrenceOrNothing()
    {
        if (!IsRecurring)
        {
            return null;
        }

        var until = RecurrenceEnds
            ? new DateTimeOffset(
                RecurrenceUntil.Date.AddDays(1).AddTicks(-1),
                TimeZoneInfo.Local.GetUtcOffset(RecurrenceUntil.Date)).ToUniversalTime()
            : (DateTimeOffset?)null;

        return new RecurrenceDto(RecurrenceFrequency, Math.Max(1, RecurrenceIntervalCount), until);
    }

    /// <summary>
    /// Where this happens, in the shape an event can store - a point first, with the name beside it.
    ///
    /// A name typed with no point behind it is looked up before this is asked, so that typing a place
    /// and saving keeps it. Without that step the box invited a name and the save quietly dropped it:
    /// the only way to attach a point was "Use my location", which answers a different question.
    /// </summary>
    private EventLocationDto? LocationOrNothing()
        => _locationLatitude is { } latitude && _locationLongitude is { } longitude
            ? new EventLocationDto(
                LocationAddress.Trim() is { Length: > 0 } address ? address : null, latitude, longitude)
            : null;

    /// <summary>
    /// Finds where a typed place is, when somebody typed one and no point is known yet. Leaves a name
    /// nothing could be found for alone and answers false, so the save can say the place was not kept
    /// rather than emptying the box on the next open.
    /// </summary>
    private async Task<bool> TryFindTheTypedPlaceAsync(CancellationToken cancellationToken)
    {
        if (LocationAddress.Trim() is not { Length: > 0 } typed || _locationLatitude is not null)
        {
            return true;
        }

        try
        {
            if (await _places.SearchAsync(typed, limit: 1, cancellationToken) is not [var found, ..])
            {
                return false;
            }

            _locationLatitude = found.Latitude;
            _locationLongitude = found.Longitude;
            OnPropertyChanged(nameof(HasLocation));
            OnPropertyChanged(nameof(LocationInGoogleMapsUrl));
            OnPropertyChanged(nameof(LocationDirectionsUrl));
            OnPropertyChanged(nameof(CanOpenLocationInGoogleMaps));
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // Nothing to look it up with. Said out loud by the caller rather than dropped in silence.
            return false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_loaded is not { } current)
        {
            return;
        }

        // Before the event is built, because a name with no point behind it cannot be stored on one.
        var placeWasFound = await TryFindTheTypedPlaceAsync(cancellationToken);

        var details = current with
        {
            Title = Title.Trim(),
            Description = Description.Trim() is { Length: > 0 } description ? description : null,
            Location = LocationOrNothing(),
            Recurrence = RecurrenceOrNothing(),
            Guests = [.. Guests.Select(guest => guest.UserId)],
            Color = _colour,
            ReminderMinutesBeforeStart = [.. Reminders.Select(reminder => reminder.MinutesBefore)],
            ReminderNotificationChannel = ReminderChannel?.Value ?? current.ReminderNotificationChannel,
            StartUtc = ChosenStartUtc,
            EndUtc = ChosenEndUtc,
            IsAllDay = IsAllDay,
            Priority = ChosenPriority.Value
        };

        var outcome = await _events.UpdateAsync(_localId, details, cancellationToken);
        if (outcome.WasRefused())
        {
            Status = outcome.Explain(RefusalMessage, _translations);
            return;
        }

        await ShowStoredEventAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);

        // After the sync, which reports on itself: said first, this would be wiped by it and the reader
        // would never learn the place did not stick.
        if (!placeWasFound)
        {
            Status = _translations[PlaceNotFoundMessage];
        }
    }

    /// <summary>The dictionary key, not the text itself - see <see cref="Translations"/>.</summary>
    private const string PlaceNotFoundMessage =
        "Saved, but that place could not be found - use your location to keep a point for it.";

    [RelayCommand]
    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        var deletion = await _events.DeleteAsync(_localId, cancellationToken);
        if (deletion.WasRefused())
        {
            Status = deletion.Explain(RefusalMessage, _translations);
            return;
        }

        await SynchroniseAsync(cancellationToken);
        _navigator.ShowCalendar();
    }

    [RelayCommand]
    private void GoBack() => _navigator.ShowCalendar();

    private async Task ShowStoredEventAsync(CancellationToken cancellationToken)
    {
        if (await _events.FindAsync(_localId, cancellationToken) is not { } calendarEvent)
        {
            _navigator.ShowCalendar();
            return;
        }

        _loaded = calendarEvent.Details;
        if (calendarEvent.ServerId is { } serverId)
        {
            Share.Describes(
                SharedItemKind.CalendarEvent, serverId, calendarEvent.Details.Title,
                calendarEvent.AccessLevel == "CanEdit" ? null : calendarEvent.OwnerUserId);
        }

        Title = calendarEvent.Details.Title;
        Description = calendarEvent.Details.Description ?? string.Empty;

        var start = calendarEvent.Details.StartUtc.ToLocalTime();
        var end = calendarEvent.Details.EndUtc.ToLocalTime();
        StartDate = start.Date;
        StartTime = start.TimeOfDay;
        // An all-day event ends at midnight the next day, which reads as one day too many on a picker.
        EndDate = calendarEvent.Details.IsAllDay ? end.Date.AddDays(-1) : end.Date;
        EndTime = end.TimeOfDay;
        IsAllDay = calendarEvent.Details.IsAllDay;

        ChosenPriority = Tasks.PriorityChoice.For(calendarEvent.Details.Priority, _translations);

        _colour = calendarEvent.Details.Color;
        ShowColours();

        var contacts = await _contacts.GetContactsAsync(cancellationToken);
        Guests.Clear();
        foreach (var guestUserId in calendarEvent.Details.Guests)
        {
            // Somebody invited from another device need not be a contact of this phone's; their id is
            // still the truth about who is coming, so they are listed as the id rather than dropped.
            Guests.Add(new GuestRow(
                guestUserId,
                contacts.FirstOrDefault(contact => contact.UserId == guestUserId)?.DisplayName
                    ?? _translations["Somebody else"]));
        }

        ShowWhoCouldBeInvited(contacts);

        Reminders.Clear();
        foreach (var minutes in calendarEvent.Details.ReminderMinutesBeforeStart.OrderBy(minutes => minutes))
        {
            Reminders.Add(new ReminderRow(minutes, ReminderChoice.Describe(minutes, _translations)));
        }

        ReminderChannel = NotificationChannelChoice.For(Channels, calendarEvent.Details.ReminderNotificationChannel);

        IsRecurring = calendarEvent.Details.Recurrence is not null;
        RecurrenceFrequency = calendarEvent.Details.Recurrence?.Frequency ?? "Weekly";
        RecurrenceIntervalCount = calendarEvent.Details.Recurrence?.IntervalCount ?? 1;
        RecurrenceEnds = calendarEvent.Details.Recurrence?.UntilUtc is not null;
        RecurrenceUntil = calendarEvent.Details.Recurrence?.UntilUtc?.ToLocalTime().Date ?? DateTime.Today;
        OnPropertyChanged(nameof(ChosenFrequency));

        LocationAddress = calendarEvent.Details.Location?.Address ?? string.Empty;
        _locationLatitude = calendarEvent.Details.Location?.Latitude;
        _locationLongitude = calendarEvent.Details.Location?.Longitude;
        OnPropertyChanged(nameof(HasLocation));
        OnPropertyChanged(nameof(LocationInGoogleMapsUrl));
        OnPropertyChanged(nameof(LocationDirectionsUrl));
        OnPropertyChanged(nameof(CanOpenLocationInGoogleMaps));

        // Asked of the store rather than decided here, so the screen and the write agree by construction.
        HasHistory = (await _events.GetHistoryOfAsync(_localId, cancellationToken)).Count > 0;
        IsReadOnly = !await _events.CanEditAsync(_localId, cancellationToken);
        // Said in the same words the row on the list before it used - being told it cannot be
        // changed, without being told why, leaves a screen that simply looks broken.
        ReadOnlyReason = OfflineEditExplanation.For(
            calendarEvent, OfflineEditPolicy.Evaluate(calendarEvent, _networkStatus), hasUnsentChanges: false,
            _translations);
        // A copy is for editing offline what could be edited online - see TaskListDetailViewModel.
        IsCopyOffered = IsReadOnly && calendarEvent.CopyOfLocalId is null && SharedItemAccess.AllowsEditing(calendarEvent);

        if (!IsReadOnly && calendarEvent.ServerId is { } lockedServerId)
        {
            // Claimed for as long as this screen is open, so somebody editing the same thing on the web
            // is told rather than left to have their save refused - see EditLock.
            await _editLock.HoldAsync(_calendarClient, lockedServerId, cancellationToken);
            ShowWhoElseIsEditing();
        }
        SaveCommand.NotifyCanExecuteChanged();
    }

    /// <inheritdoc cref="Notes.NoteDetailViewModel"/>
    private async Task SynchroniseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _synchronizer.SynchroniseAsync(cancellationToken);
            Status = result.ReachedTheServer
                ? string.Empty
                : _translations["Saved on this phone - it will sync later"];
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Status = _translations["Saved on this phone - it will sync later"];
        }
    }

    /// <summary>The dictionary key, not the text itself - see <see cref="Translations"/>.</summary>
    private const string RefusalMessage =
        "Somebody else can change this event, and Orbit can't be reached to check. It stays read-only until you're back online.";

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnTitleChanged(string value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanAddToGoogleCalendar));
    }

    /// <summary>The offer appears the moment the answer arrives, not on the next keystroke.</summary>
    partial void OnHasGoogleExtrasChanged(bool value)
    {
        OnPropertyChanged(nameof(CanAddToGoogleCalendar));
        OnPropertyChanged(nameof(CanOpenLocationInGoogleMaps));
    }

    partial void OnRecurrenceFrequencyChanged(string value) => OnPropertyChanged(nameof(ChosenFrequency));

    partial void OnIsReadOnlyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEdit));
        SaveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Why it cannot be changed right now - empty when it can, which is the common case.</summary>
    [ObservableProperty]
    private string _readOnlyReason = string.Empty;

    public bool HasReadOnlyReason => ReadOnlyReason.Length > 0;

    private void ShowWhoElseIsEditing()
    {
        if (!_editLock.IsHeldByAnother)
        {
            return;
        }

        IsReadOnly = true;
        ReadOnlyReason = _editLock.RefusalMessage;
    }

    /// <summary>Lets it go when the screen does, rather than leaving it claimed for a minute.</summary>
    public Task CloseAsync() => _editLock.ReleaseAsync();

    partial void OnReadOnlyReasonChanged(string value) => OnPropertyChanged(nameof(HasReadOnlyReason));

    /// <inheritdoc cref="Notes.NoteDetailViewModel.IsCopyOffered"/>
    [ObservableProperty]
    private bool _isCopyOffered;

    /// <inheritdoc cref="Notes.NoteDetailViewModel.CopyForEditingAsync"/>
    [RelayCommand]
    private async Task CopyForEditingAsync(CancellationToken cancellationToken)
    {
        if (await _events.CopyForEditingAsync(_localId, cancellationToken) is not { } copy)
        {
            return;
        }

        IsCopyOffered = false;
        _navigator.ShowCalendarEvent(copy.LocalId);
    }

    /// <inheritdoc cref="Notes.NoteDetailViewModel.DeclineCopy"/>
    [RelayCommand]
    private void DeclineCopy() => IsCopyOffered = false;

    /// <summary>
    /// Whether anything was ever copied from this - what puts its history within reach. Hidden until
    /// there is one, because most things have none and a permanent link to an empty window is clutter.
    /// </summary>
    [ObservableProperty]
    private bool _hasHistory;

    /// <summary>This thing's own history, opened from this thing - see CopyHistoryViewModel.</summary>
    [RelayCommand]
    private void GoToHistory() => _navigator.ShowCopyHistory(CopyKind.CalendarEvent, _localId);
}
