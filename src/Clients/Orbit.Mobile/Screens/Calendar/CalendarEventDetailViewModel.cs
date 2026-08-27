using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Calendar;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Screens.Sharing;
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

    [ObservableProperty]
    private TimeSpan _endTime;

    [ObservableProperty]
    private bool _isAllDay;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isReadOnly;

    public CalendarEventDetailViewModel(
        LocalCalendarEventRepository events, CalendarEventSynchronizer synchronizer, Translations translations,
        SharePanel share, IScreenNavigator navigator)
    {
        _events = events;
        _synchronizer = synchronizer;
        _translations = translations;
        Share = share;
        _navigator = navigator;
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

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_loaded is not { } current)
        {
            return;
        }

        var start = StartDate.Date + (IsAllDay ? TimeSpan.Zero : StartTime);
        var end = StartDate.Date + (IsAllDay ? TimeSpan.FromDays(1) : EndTime);

        var details = current with
        {
            Title = Title.Trim(),
            Description = Description.Trim() is { Length: > 0 } description ? description : null,
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
        EndTime = end.TimeOfDay;
        IsAllDay = calendarEvent.Details.IsAllDay;

        // Asked of the store rather than decided here, so the screen and the write agree by construction.
        IsReadOnly = !await _events.CanEditAsync(_localId, cancellationToken);
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

    partial void OnIsReadOnlyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEdit));
        SaveCommand.NotifyCanExecuteChanged();
    }
}
