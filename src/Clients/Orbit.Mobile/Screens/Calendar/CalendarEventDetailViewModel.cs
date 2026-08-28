using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Calendar;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
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
    private readonly CalendarEventSynchronizer _synchronizer;
    private readonly CalendarClient _calendarClient;
    private readonly EditLock _editLock;
    private readonly IDeviceLocation _deviceLocation;
    private readonly Translations _translations;
    private readonly IScreenNavigator _navigator;

    private Guid _localId;

    /// <summary>
    /// Everything this screen does not show. Kept whole and sent back unchanged, which is what stops an
    /// edit here from being a quiet deletion of what the browser set.
    /// </summary>
    private CalendarEventDetailsDto? _loaded;

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

    public CalendarEventDetailViewModel(
        LocalCalendarEventRepository events, CalendarEventSynchronizer synchronizer, Translations translations,
        SharePanel share, IScreenNavigator navigator,
        CalendarClient calendarClient, EditLock editLock, IDeviceLocation deviceLocation)
    {
        _events = events;
        _synchronizer = synchronizer;
        _translations = translations;
        Share = share;
        _navigator = navigator;
        _calendarClient = calendarClient;
        _editLock = editLock;
        _deviceLocation = deviceLocation;
        Frequencies = RecurrenceChoice.All(translations);
        _editLock.Changed += (_, _) => ShowWhoElseIsEditing();
    }

    /// <summary>Offering this to somebody else - see SharePanel.</summary>
    public SharePanel Share { get; }

    public bool HasStatus => Status.Length > 0;

    public bool CanEdit => !IsReadOnly;

    /// <summary>False until a load has succeeded: saving what was never read would write guesses.</summary>
    public bool CanSave => _loaded is not null && CanEdit && Title.Trim().Length > 0;

    public void Open(Guid localId) => _localId = localId;

    [RelayCommand]
    private Task LoadAsync(CancellationToken cancellationToken) => ShowStoredEventAsync(cancellationToken);

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
        await SaveAsync(cancellationToken);
    }

    [RelayCommand]
    private Task RemoveLocationAsync(CancellationToken cancellationToken)
    {
        _locationLatitude = null;
        _locationLongitude = null;
        LocationAddress = string.Empty;
        OnPropertyChanged(nameof(HasLocation));
        return SaveAsync(cancellationToken);
    }

    /// <summary>
    /// A location is a point with an optional label, so an address typed with no point behind it is not
    /// one and is not sent - the same rule Orbit.Web's editor applies.
    /// </summary>
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

    private EventLocationDto? LocationOrNothing()
        => _locationLatitude is { } latitude && _locationLongitude is { } longitude
            ? new EventLocationDto(
                LocationAddress.Trim() is { Length: > 0 } address ? address : null, latitude, longitude)
            : null;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_loaded is not { } current)
        {
            return;
        }

        var start = StartDate.Date + (IsAllDay ? TimeSpan.Zero : StartTime);
        var end = IsAllDay ? EndDate.Date + TimeSpan.FromDays(1) : EndDate.Date + EndTime;

        var details = current with
        {
            Title = Title.Trim(),
            Description = Description.Trim() is { Length: > 0 } description ? description : null,
            Location = LocationOrNothing(),
            Recurrence = RecurrenceOrNothing(),
            StartUtc = new DateTimeOffset(start, TimeZoneInfo.Local.GetUtcOffset(start)).ToUniversalTime(),
            EndUtc = new DateTimeOffset(end, TimeZoneInfo.Local.GetUtcOffset(end)).ToUniversalTime(),
            IsAllDay = IsAllDay
        };

        if (await _events.UpdateAsync(_localId, details, cancellationToken) is LocalWriteOutcome.RefusedWhileOffline)
        {
            Status = _translations[RefusalMessage];
            return;
        }

        await ShowStoredEventAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (await _events.DeleteAsync(_localId, cancellationToken) is LocalWriteOutcome.RefusedWhileOffline)
        {
            Status = _translations[RefusalMessage];
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

        // Asked of the store rather than decided here, so the screen and the write agree by construction.
        IsReadOnly = !await _events.CanEditAsync(_localId, cancellationToken);
        ReadOnlyReason = string.Empty;

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

    partial void OnTitleChanged(string value) => SaveCommand.NotifyCanExecuteChanged();

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
}
