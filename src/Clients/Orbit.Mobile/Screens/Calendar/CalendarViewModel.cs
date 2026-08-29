using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Calendar;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
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
    private readonly LocalTaskListRepository _taskLists;
    private readonly CalendarEventSynchronizer _synchronizer;
    private readonly INetworkStatus _networkStatus;
    private readonly TimeProvider _timeProvider;
    private readonly SyncState _syncState;
    private readonly IScreenNavigator _navigator;
    private readonly Translations _translations;

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
        TimeProvider timeProvider, SyncState syncState, IScreenNavigator navigator, Translations translations,
        LocalTaskListRepository taskLists)
    {
        _events = events;
        _taskLists = taskLists;
        _synchronizer = synchronizer;
        _networkStatus = networkStatus;
        _timeProvider = timeProvider;
        _syncState = syncState;
        _navigator = navigator;
        _translations = translations;
    }

    public ObservableCollection<CalendarEventRow> Events { get; } = [];

    /// <summary>
    /// What falls due over the same stretch of days, beneath the events. Read-only here: a deadline
    /// belongs to the list it sits on, and tapping it opens that list, which is where it can be ticked.
    /// </summary>
    public ObservableCollection<CalendarDeadline> Deadlines { get; } = [];

    public bool HasDeadlines => Deadlines.Count > 0;

    /// <summary>The month grid - six weeks of seven days, whatever month it is. See CalendarMonth.</summary>
    public ObservableCollection<CalendarDay> Days { get; } = [];

    /// <summary>The twelve months of the year being shown, for the year overview. See CalendarYear.</summary>
    public ObservableCollection<CalendarYearMonth> Months { get; } = [];

    public IReadOnlyList<string> WeekdayNames => CalendarMonth.WeekdayNames(_translations);

    /// <summary>Which month the grid is showing, which is not necessarily the month containing today.</summary>
    [ObservableProperty]
    private DateTime _month = DateTime.Today;

    /// <summary>
    /// The day the list beneath the grid is showing, or null for the whole month. Null rather than
    /// today by default: opening the calendar on a month with one event on the 3rd should show it.
    /// </summary>
    [ObservableProperty]
    private DateTime? _selectedDay;

    /// <summary>
    /// Whether the year overview is showing in place of the month grid. Orbit.Web switches between day,
    /// month and year; here the day is the month grid with one of its days chosen, so this is the whole
    /// of the switch.
    /// </summary>
    [ObservableProperty]
    private bool _isShowingYear;

    /// <summary>What the header says above the grid: a month, or a year when the year is showing.</summary>
    public string PeriodLabel
        => IsShowingYear ? CalendarYear.Describe(Month.Year) : CalendarMonth.Describe(Month, _translations);

    public bool IsShowingMonth => !IsShowingYear;

    public bool IsShowingOneDay => SelectedDay is not null;

    /// <summary>A step back through whatever is on screen: a month, or a year when the year is showing.</summary>
    [RelayCommand]
    private Task ShowEarlierAsync(CancellationToken cancellationToken) => StepAsync(-1, cancellationToken);

    [RelayCommand]
    private Task ShowLaterAsync(CancellationToken cancellationToken) => StepAsync(1, cancellationToken);

    private Task StepAsync(int direction, CancellationToken cancellationToken)
    {
        Month = IsShowingYear ? Month.AddYears(direction) : Month.AddMonths(direction);
        SelectedDay = null;
        return ShowStoredEventsAsync(cancellationToken);
    }

    /// <summary>The year overview, and back out of it. Choosing a month is the way out that goes somewhere.</summary>
    [RelayCommand]
    private Task ShowYearAsync(CancellationToken cancellationToken)
    {
        IsShowingYear = true;
        SelectedDay = null;
        return ShowStoredEventsAsync(cancellationToken);
    }

    [RelayCommand]
    private Task ShowMonthAsync(CancellationToken cancellationToken)
    {
        IsShowingYear = false;
        return ShowStoredEventsAsync(cancellationToken);
    }

    /// <summary>Tapping a month in the year overview opens it, which is what the overview is for.</summary>
    [RelayCommand]
    private Task ChooseMonthAsync(CalendarYearMonth? month, CancellationToken cancellationToken)
    {
        if (month is null)
        {
            return Task.CompletedTask;
        }

        Month = month.Month;
        IsShowingYear = false;
        return ShowStoredEventsAsync(cancellationToken);
    }

    /// <summary>Back to the month containing today, and to the whole of it.</summary>
    [RelayCommand]
    private Task ShowTodayAsync(CancellationToken cancellationToken)
    {
        Month = _timeProvider.GetUtcNow().LocalDateTime.Date;
        SelectedDay = null;
        IsShowingYear = false;
        return ShowStoredEventsAsync(cancellationToken);
    }

    /// <summary>
    /// Tapping a day narrows the list to it; tapping it again widens back to the month. A day outside
    /// the month being shown moves the grid to its own month, which is what tapping it means.
    /// </summary>
    [RelayCommand]
    private Task ChooseDayAsync(CalendarDay? day, CancellationToken cancellationToken)
    {
        if (day is null)
        {
            return Task.CompletedTask;
        }

        SelectedDay = SelectedDay == day.Date ? null : day.Date;
        Month = day.Date;
        IsShowingYear = false;
        return ShowStoredEventsAsync(cancellationToken);
    }

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

    /// <summary>Opens one event, as the notes list opens one note.</summary>
    [RelayCommand]
    private void Open(CalendarEventRow? row)
    {
        if (row is not null)
        {
            _navigator.ShowCalendarEvent(row.LocalId);
        }
    }

    /// <summary>
    /// Somewhere to get to opens on its own, with what it is, when it is and where; everything else
    /// opens the list it sits on, which is where it gets ticked off. Orbit.Web's calendar splits them
    /// the same way and for the same reason - see CalendarDeadline.IsSomewhere.
    /// </summary>
    [RelayCommand]
    private void OpenDeadline(CalendarDeadline? deadline)
    {
        if (deadline is null)
        {
            return;
        }

        if (deadline.IsSomewhere)
        {
            _navigator.ShowTaskItem(deadline.TaskListLocalId, deadline.ItemId);
            return;
        }

        _navigator.ShowTaskList(deadline.TaskListLocalId);
    }

    private async Task ShowStoredEventsAsync(CancellationToken cancellationToken)
    {
        var stored = await _events.GetAllAsync(cancellationToken);
        var pending = await _events.GetPendingLocalIdsAsync(cancellationToken);
        var today = _timeProvider.GetUtcNow().LocalDateTime;
        var deadlines = CalendarDeadline.From(
            await _taskLists.GetAllAsync(cancellationToken), stored, _translations);

        Days.Clear();
        foreach (var day in CalendarMonth.Build(Month, SelectedDay, today, stored, deadlines))
        {
            Days.Add(day);
        }

        Months.Clear();
        foreach (var month in CalendarYear.Build(Month.Year, today, stored, deadlines, _translations))
        {
            Months.Add(month);
        }

        // The list beneath the grid follows it: the chosen day, or the whole month when none is chosen.
        var shown = stored.Where(calendarEvent => Covers(calendarEvent.Details.StartUtc.ToLocalTime().Date));

        Events.Clear();
        foreach (var calendarEvent in shown)
        {
            Events.Add(CalendarEventRow.From(calendarEvent, pending.Contains(calendarEvent.LocalId), _networkStatus, _translations));
        }

        Deadlines.Clear();
        foreach (var deadline in deadlines.Where(deadline => Covers(deadline.DueLocalDate)))
        {
            Deadlines.Add(deadline);
        }

        OnPropertyChanged(nameof(PeriodLabel));
        OnPropertyChanged(nameof(IsShowingOneDay));
        OnPropertyChanged(nameof(HasDeadlines));
    }

    private bool Covers(DateTime date)
    {
        if (SelectedDay is { } chosen)
        {
            return date == chosen.Date;
        }

        return IsShowingYear
            ? date.Year == Month.Year
            : date.Month == Month.Month && date.Year == Month.Year;
    }

    partial void OnIsShowingYearChanged(bool value)
    {
        OnPropertyChanged(nameof(PeriodLabel));
        OnPropertyChanged(nameof(IsShowingMonth));
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
