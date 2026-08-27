using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Calendar;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Calendar;

/// <summary>
/// Upcoming events, read from the local database. Adding one here creates a plain hour-long event at a
/// chosen time; the web client's full editor - recurrence, guests, location, reminders - is a later
/// step, and the sync layer already carries all of it either way.
/// </summary>
public sealed partial class CalendarViewModel : ObservableObject
{
    private readonly LocalCalendarEventRepository _events;
    private readonly CalendarEventSynchronizer _synchronizer;
    private readonly INetworkStatus _networkStatus;
    private readonly TimeProvider _timeProvider;
    private readonly SyncState _syncState;
    private readonly IScreenNavigator _navigator;

    [ObservableProperty]
    private string _newEventTitle = string.Empty;

    [ObservableProperty]
    private DateTime _newEventDate = DateTime.Today;

    [ObservableProperty]
    private TimeSpan _newEventTime = new(9, 0, 0);

    [ObservableProperty]
    private bool _isRefreshing;

    public CalendarViewModel(
        LocalCalendarEventRepository events, CalendarEventSynchronizer synchronizer, INetworkStatus networkStatus,
        TimeProvider timeProvider, SyncState syncState, IScreenNavigator navigator)
    {
        _events = events;
        _synchronizer = synchronizer;
        _networkStatus = networkStatus;
        _timeProvider = timeProvider;
        _syncState = syncState;
        _navigator = navigator;
    }

    public ObservableCollection<CalendarEventRow> Events { get; } = [];

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        await ShowStoredEventsAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanAddEvent))]
    private async Task AddEventAsync(CancellationToken cancellationToken)
    {
        // The pickers show local time, and the wire is UTC - converted here rather than sent as a local
        // offset, because Npgsql refuses a DateTimeOffset with a non-zero offset for a "timestamp with
        // time zone" column outright. Orbit.Web was bitten by exactly this in its own editors.
        var localStart = new DateTimeOffset(NewEventDate.Date + NewEventTime, TimeZoneInfo.Local.GetUtcOffset(NewEventDate));
        var start = localStart.ToUniversalTime();
        await _events.CreateAsync(
            new CalendarEventDetailsDto(
                NewEventTitle.Trim(), null, null, null, start, start.AddHours(1), false, null, [], [], "None", "None"),
            cancellationToken);

        NewEventTitle = string.Empty;
        await ShowStoredEventsAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    private bool CanAddEvent => NewEventTitle.Trim().Length > 0;

    [RelayCommand]
    private void GoBack() => _navigator.ShowDashboard();

    private async Task ShowStoredEventsAsync(CancellationToken cancellationToken)
    {
        var stored = await _events.GetAllAsync(cancellationToken);
        var pending = await _events.GetPendingLocalIdsAsync(cancellationToken);

        Events.Clear();
        foreach (var calendarEvent in stored)
        {
            Events.Add(CalendarEventRow.From(calendarEvent, pending.Contains(calendarEvent.LocalId), _networkStatus));
        }
    }

    private async Task SynchroniseAsync(CancellationToken cancellationToken)
    {
        IsRefreshing = true;
        _syncState.RecordStarted();
        try
        {
            var result = await _synchronizer.SynchroniseAsync(cancellationToken);
            RecordSync(result);

            if (result.Sent + result.Received + result.RemovedLocally > 0)
            {
                await ShowStoredEventsAsync(cancellationToken);
            }
        }
        catch (HttpRequestException)
        {
            _syncState.RecordFailed();
        }
        catch (OperationCanceledException)
        {
            // The screen went away mid-sync.
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>"Offline" is only said when the phone actually believes it has no connection.</summary>
    /// <summary>
    /// A sync that never reached the server is not the same as one the server refused, and SyncState
    /// tells them apart from the phone's own belief about connectivity rather than from the result.
    /// </summary>
    private void RecordSync(SyncResult result)
    {
        if (result.ReachedTheServer)
        {
            _syncState.RecordSucceeded();
            return;
        }

        _syncState.RecordFailed();
    }
    partial void OnNewEventTitleChanged(string value) => AddEventCommand.NotifyCanExecuteChanged();
}
