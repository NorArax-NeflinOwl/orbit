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
    private readonly ICalendarListOrderStore _listOrder;

    /// <summary>What is on the shown days, before it is put in the reader's chosen order.</summary>
    private readonly List<CalendarListEntry> _listed = [];

    [ObservableProperty]
    private string _newEventTitle = string.Empty;

    /// <summary>Set from the injected clock in the constructor - see there.</summary>
    [ObservableProperty]
    private DateTime _newEventDate;

    [ObservableProperty]
    private TimeSpan _newEventTime = new(9, 0, 0);

    [ObservableProperty]
    private bool _isRefreshing;

    public CalendarViewModel(
        LocalCalendarEventRepository events, CalendarEventSynchronizer synchronizer, INetworkStatus networkStatus,
        TimeProvider timeProvider, SyncState syncState, IScreenNavigator navigator, Translations translations,
        LocalTaskListRepository taskLists, ICalendarListOrderStore listOrder)
    {
        _events = events;
        _listOrder = listOrder;
        var reading = listOrder.Read();
        _sortOrder = reading.SortOrder;
        _showsEverything = reading.ShowsEverything;
        _taskLists = taskLists;
        _synchronizer = synchronizer;
        _networkStatus = networkStatus;
        _timeProvider = timeProvider;
        _syncState = syncState;
        _navigator = navigator;
        _translations = translations;

        // Both of these are "today", and today is whatever the injected clock says - not what the
        // machine says. Assigned here rather than where the fields are declared, because a field
        // initialiser runs before the constructor and so cannot reach the clock; reaching for
        // DateTime.Today there instead meant this screen was the one part of the calendar that ignored
        // the clock it was given, and it decided the only thing that matters: which month opens.
        var today = _timeProvider.GetUtcNow().LocalDateTime.Date;
        _month = today;
        _newEventDate = today;
    }

    /// <summary>
    /// Everything happening over the shown days, whichever kind it is - see CalendarListEntry for why
    /// appointments and deadlines share one list. A deadline is read-only here: it belongs to the list
    /// it sits on, and tapping it opens that list, which is where it can be ticked.
    /// </summary>
    public ObservableCollection<CalendarListEntry> Listed { get; } = [];

    /// <summary>What order that list is read in, kept on this device - see ICalendarListOrderStore.</summary>
    [ObservableProperty]
    private CalendarListSortOrder _sortOrder;

    /// <summary>
    /// Whether the list also holds what is already over - see CalendarListEntry.IsOver. Kept beside the
    /// order, because both describe how this one reader reads this one page.
    /// </summary>
    [ObservableProperty]
    private bool _showsEverything;

    /// <summary>
    /// The month grid - six weeks of seven days, whatever month it is, or the one week the reader is
    /// standing on once the calendar has been minimised. See CalendarMonth and MinimisedCalendar.
    /// </summary>
    public ObservableCollection<CalendarDay> Days { get; } = [];

    /// <summary>
    /// Whether the calendar has got out of the way, which the page turns on as the list beneath it is
    /// scrolled past it. Android only, and decided as such: a desktop window has room for the grid and
    /// the list at once - see info/future-plan.md.
    /// </summary>
    [ObservableProperty]
    private bool _isMinimised;

    /// <summary>Everything the grid holds, whatever is being shown of it right now.</summary>
    private IReadOnlyList<CalendarDay> _wholeMonth = [];

    private IReadOnlyList<CalendarYearMonth> _wholeYear = [];

    /// <summary>
    /// The chosen day laid out by the hour - see CalendarDayTimeline. Drawn under the month grid rather
    /// than in place of it: on a phone the grid is how the next day is picked, and Orbit.Web can afford
    /// to swap the two because it has a sidebar to pick from.
    /// </summary>
    public ObservableCollection<DayBlock> DayBlocks { get; } = [];

    /// <summary>What has no hour to be drawn at, in a row of its own above the clock.</summary>
    public ObservableCollection<DayBlock> AllDayBlocks { get; } = [];

    /// <summary>Nothing on a day is worth an empty clock: the list beneath already says so.</summary>
    public bool HasDayTimeline => IsShowingOneDay && (DayBlocks.Count > 0 || AllDayBlocks.Count > 0);

    /// <summary>The twelve months of the year being shown, for the year overview. See CalendarYear.</summary>
    public ObservableCollection<CalendarYearMonth> Months { get; } = [];

    public IReadOnlyList<string> WeekdayNames => CalendarMonth.WeekdayNames(_translations);

    /// <summary>
    /// The stretch of the clock the day view draws - see CalendarDayTimeline. One hour of it once the
    /// calendar has been minimised, which is the day's answer to the week the month keeps.
    /// </summary>
    public (int FirstHour, int LastHour) HoursOnShow
        => IsMinimised
            ? MinimisedCalendar.HourOf(DayBlocks, SelectedDay ?? Month, _timeProvider.GetUtcNow().LocalDateTime)
            : CalendarDayTimeline.HoursWorthDrawing(DayBlocks);

    partial void OnIsMinimisedChanged(bool value)
    {
        ShowTheGrid();
        OnPropertyChanged(nameof(HoursOnShow));
    }

    /// <summary>
    /// Fills the grid from what was last read, taking the minimising into account - so getting out of
    /// the way and coming back is a redraw rather than another read of the store.
    /// </summary>
    private void ShowTheGrid()
    {
        var today = _timeProvider.GetUtcNow().LocalDateTime;

        Days.Clear();
        foreach (var day in IsMinimised
            ? MinimisedCalendar.WeekOf(_wholeMonth, SelectedDay, today)
            : _wholeMonth)
        {
            Days.Add(day);
        }

        Months.Clear();
        foreach (var month in IsMinimised ? MinimisedCalendar.MonthOf(_wholeYear, Month) : _wholeYear)
        {
            Months.Add(month);
        }
    }

    /// <summary>
    /// Which month the grid is showing, which is not necessarily the month containing today. Starts at
    /// the month the injected clock is in - set in the constructor, see there.
    /// </summary>
    [ObservableProperty]
    private DateTime _month;

    /// <summary>
    /// The day the list beneath the grid is showing, or null for the whole month. Null rather than
    /// today by default: opening the calendar on a month with one event on the 3rd should show it.
    /// </summary>
    [ObservableProperty]
    private DateTime? _selectedDay;

    /// <summary>
    /// How much of the calendar is showing - the same three the browser switches between, and now the
    /// same three here. The phone had two: a month grid whose days could be tapped, and a year. The day
    /// was reachable but never a place you could go, so "just today" meant finding today in a grid.
    /// </summary>
    [ObservableProperty]
    private CalendarViewMode _viewMode = CalendarViewMode.Month;

    /// <summary>What the header says above the grid: the day, the month, or the year that is showing.</summary>
    public string PeriodLabel
        => ViewMode switch
        {
            CalendarViewMode.Year => CalendarYear.Describe(Month.Year),
            CalendarViewMode.Day => (SelectedDay ?? Month).ToString("d MMMM yyyy", _translations.DisplayCulture),
            _ => CalendarMonth.Describe(Month, _translations)
        };

    public bool IsShowingYear => ViewMode is CalendarViewMode.Year;

    public bool IsShowingMonth => ViewMode is CalendarViewMode.Month;

    public bool IsShowingDay => ViewMode is CalendarViewMode.Day;

    /// <summary>
    /// Whether one day's worth is what the list beneath is showing - true in Day mode, and true in Month
    /// mode once a day has been tapped, which is how the phone has always narrowed a month.
    /// </summary>
    public bool IsShowingOneDay => SelectedDay is not null;

    /// <summary>A step back through whatever is on screen: a month, or a year when the year is showing.</summary>
    [RelayCommand]
    private Task ShowEarlierAsync(CancellationToken cancellationToken) => StepAsync(-1, cancellationToken);

    [RelayCommand]
    private Task ShowLaterAsync(CancellationToken cancellationToken) => StepAsync(1, cancellationToken);

    private Task StepAsync(int direction, CancellationToken cancellationToken)
    {
        // A step means one of whatever is on screen: a day in the day view, a month in the month grid,
        // a year in the overview. Stepping a month while showing one day was the old behaviour and read
        // as the arrows being broken.
        if (ViewMode is CalendarViewMode.Day)
        {
            SelectedDay = (SelectedDay ?? Month).AddDays(direction);
            Month = SelectedDay.Value;
            return ShowStoredEventsAsync(cancellationToken);
        }

        Month = ViewMode is CalendarViewMode.Year ? Month.AddYears(direction) : Month.AddMonths(direction);
        SelectedDay = null;
        return ShowStoredEventsAsync(cancellationToken);
    }

    /// <summary>
    /// One day on its own - the browser's Day view, which the phone had no way to reach. Opens on
    /// whichever day was chosen in the grid, or today when none was.
    /// </summary>
    [RelayCommand]
    private Task ShowDayAsync(CancellationToken cancellationToken)
    {
        SelectedDay ??= _timeProvider.GetUtcNow().LocalDateTime.Date;
        Month = SelectedDay.Value;
        ViewMode = CalendarViewMode.Day;
        return ShowStoredEventsAsync(cancellationToken);
    }

    /// <summary>The year overview, and back out of it. Choosing a month is the way out that goes somewhere.</summary>
    [RelayCommand]
    private Task ShowYearAsync(CancellationToken cancellationToken)
    {
        ViewMode = CalendarViewMode.Year;
        SelectedDay = null;
        return ShowStoredEventsAsync(cancellationToken);
    }

    [RelayCommand]
    private Task ShowMonthAsync(CancellationToken cancellationToken)
    {
        ViewMode = CalendarViewMode.Month;
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
        ViewMode = CalendarViewMode.Month;
        return ShowStoredEventsAsync(cancellationToken);
    }

    /// <summary>Back to the month containing today, and to the whole of it.</summary>
    [RelayCommand]
    private Task ShowTodayAsync(CancellationToken cancellationToken)
    {
        Month = _timeProvider.GetUtcNow().LocalDateTime.Date;
        SelectedDay = null;
        ViewMode = CalendarViewMode.Month;
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
        ViewMode = CalendarViewMode.Month;
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
    /// Opens whichever kind of thing was pressed, since the list holds both - an appointment opens
    /// itself, a deadline opens where it can be ticked off. See OpenDeadline for that split.
    /// </summary>
    [RelayCommand]
    private void OpenListed(CalendarListEntry? entry)
    {
        if (entry?.Event is { } calendarEvent)
        {
            Open(calendarEvent);
            return;
        }

        OpenDeadline(entry?.Deadline);
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
        var pending = await _events.GetPendingLocalIdsAsync(cancellationToken);
        var today = _timeProvider.GetUtcNow().LocalDateTime;
        var stored = OnTheDaysTheyFallOn(await _events.GetAllAsync(cancellationToken));
        var deadlines = CalendarDeadline.From(
            await _taskLists.GetAllAsync(cancellationToken), stored, _translations);

        _wholeMonth = CalendarMonth.Build(Month, SelectedDay, today, stored, deadlines);
        _wholeYear = CalendarYear.Build(Month.Year, today, stored, deadlines, _translations);
        ShowTheGrid();

        // The list beneath the grid follows it: the chosen day, or the whole month when none is chosen.
        var shown = stored.Where(calendarEvent => Covers(calendarEvent.Details.StartUtc.ToLocalTime().Date));

        ShowListed(
            shown.Select(calendarEvent => CalendarListEntry.For(
                CalendarEventRow.From(
                    calendarEvent, pending.Contains(calendarEvent.LocalId), _networkStatus, _translations)))
            .Concat(deadlines
                .Where(deadline => Covers(deadline.DueLocalDate))
                .Select(CalendarListEntry.For)));

        ShowTheChosenDay(stored);

        OnPropertyChanged(nameof(PeriodLabel));
        OnPropertyChanged(nameof(IsShowingOneDay));
        OnPropertyChanged(nameof(HasDayTimeline));
    }

    /// <summary>
    /// Keeps what is on the shown days, then draws it in whatever order was chosen. Held apart from the
    /// collection the screen reads so that changing the order re-sorts what is already there rather
    /// than sending the screen back to the database for it.
    /// </summary>
    private void ShowListed(IEnumerable<CalendarListEntry> entries)
    {
        _listed.Clear();
        _listed.AddRange(entries);
        ShowInChosenOrder();
    }

    private void ShowInChosenOrder()
    {
        // What a calendar is read for is what is coming, so what is over is left off unless it was
        // asked for. The grid beside the list still draws it - see CalendarListEntry.IsOver.
        var nowUtc = _timeProvider.GetUtcNow();
        var worthShowing = ShowsEverything
            ? _listed
            : [.. _listed.Where(entry => !entry.IsOver(nowUtc))];

        Listed.Clear();
        foreach (var entry in CalendarListEntry.InOrder(worthShowing, SortOrder))
        {
            Listed.Add(entry);
        }
    }

    partial void OnSortOrderChanged(CalendarListSortOrder value)
    {
        Remember();
        ShowInChosenOrder();
    }

    partial void OnShowsEverythingChanged(bool value)
    {
        Remember();
        ShowInChosenOrder();
    }

    private void Remember() => _listOrder.Write(new CalendarListReading(SortOrder, ShowsEverything));

    /// <summary>
    /// A repeating event as every day it lands on - see <see cref="CalendarOccurrences"/>.
    ///
    /// Expanded over the whole displayed year, because the year grid is drawn from the same list as the
    /// month one, and a week either side of it: a month grid always shows six full weeks, so January's
    /// spills back into the December before and December's forward into the January after.
    /// </summary>
    private IReadOnlyList<LocalCalendarEvent> OnTheDaysTheyFallOn(IReadOnlyList<LocalCalendarEvent> stored)
    {
        var yearStart = new DateTimeOffset(new DateTime(Month.Year, 1, 1), TimeSpan.Zero);
        return CalendarOccurrences.Between(stored, yearStart.AddDays(-7), yearStart.AddYears(1).AddDays(7));
    }

    /// <summary>
    /// The chosen day on the clock. Nothing to draw when no day is chosen: the month grid is showing a
    /// month, and an hour timeline of thirty days would be a wall.
    /// </summary>
    private void ShowTheChosenDay(IReadOnlyList<LocalCalendarEvent> events)
    {
        DayBlocks.Clear();
        AllDayBlocks.Clear();
        if (SelectedDay is not { } day)
        {
            return;
        }

        foreach (var block in CalendarDayTimeline.AllDayOn(day, events, _translations))
        {
            AllDayBlocks.Add(block);
        }

        foreach (var block in CalendarDayTimeline.Build(day, events, _translations))
        {
            DayBlocks.Add(block);
        }
    }

    /// <summary>Opens the event a block stands for - the same event the list beneath it opens.</summary>
    [RelayCommand]
    private void OpenBlock(DayBlock? block)
    {
        if (block is not null)
        {
            _navigator.ShowCalendarEvent(block.LocalId);
        }
    }

    private bool Covers(DateTime date)
    {
        if (SelectedDay is { } chosen)
        {
            return date == chosen.Date;
        }

        return ViewMode is CalendarViewMode.Year
            ? date.Year == Month.Year
            : date.Month == Month.Month && date.Year == Month.Year;
    }

    partial void OnViewModeChanged(CalendarViewMode value)
    {
        OnPropertyChanged(nameof(PeriodLabel));
        OnPropertyChanged(nameof(IsShowingMonth));
        OnPropertyChanged(nameof(IsShowingYear));
        OnPropertyChanged(nameof(IsShowingDay));
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
