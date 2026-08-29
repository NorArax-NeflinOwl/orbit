using Orbit.Contracts.Calendar;

namespace Orbit.Web.Services;

/// <summary>
/// Turns the flat list of calendar events the API returns into the week/day grids Calendar.razor and its
/// month/day/year subcomponents render, keeping the date arithmetic and event-placement math testable
/// independently of the Razor markup.
/// </summary>
public static class CalendarGridBuilder
{
    public const int MinutesPerDay = 24 * 60;

    /// <summary>
    /// Builds the grid for the month containing monthReferenceDate: a whole number of Monday-to-Sunday
    /// weeks, starting on the Monday on or before the 1st and ending on the Sunday on or after the last
    /// day of the month, so leading/trailing days from the neighboring months fill out the first/last row.
    /// </summary>
    public static IReadOnlyList<MonthGridWeek> BuildMonthGrid(
        DateOnly monthReferenceDate, IReadOnlyList<CalendarEventDto> events, IReadOnlyList<DueTaskDto> dueTasks)
    {
        var firstOfMonth = new DateOnly(monthReferenceDate.Year, monthReferenceDate.Month, 1);
        var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);
        var gridStart = firstOfMonth.AddDays(-DaysSinceMonday(firstOfMonth.DayOfWeek));
        var gridEnd = lastOfMonth.AddDays(DaysUntilSunday(lastOfMonth.DayOfWeek));
        var expandedEvents = ExpandRecurringEvents(events, gridStart, gridEnd);

        var weeks = new List<MonthGridWeek>();
        for (var weekStart = gridStart; weekStart <= gridEnd; weekStart = weekStart.AddDays(7))
        {
            var days = Enumerable.Range(0, 7)
                .Select(dayOffset => BuildMonthGridDay(weekStart.AddDays(dayOffset), monthReferenceDate.Month, expandedEvents, dueTasks))
                .ToList();
            weeks.Add(new MonthGridWeek(days));
        }

        return weeks;
    }

    /// <summary>Builds all 12 of a year's month grids in one call, for the year view.</summary>
    public static IReadOnlyList<YearGridMonth> BuildYearGrid(int year, IReadOnlyList<CalendarEventDto> events, IReadOnlyList<DueTaskDto> dueTasks)
        => Enumerable.Range(1, 12)
            .Select(month => new YearGridMonth(month, BuildMonthGrid(new DateOnly(year, month, 1), events, dueTasks)))
            .ToList();

    /// <summary>
    /// Splits day's events into an all-day strip and a set of timed events placed on a minute-precision
    /// timeline (see DayGridPlacedEvent), with simultaneous events assigned side-by-side columns instead
    /// of overlapping each other.
    /// </summary>
    public static DayGrid BuildDayGrid(DateOnly day, IReadOnlyList<CalendarEventDto> events, IReadOnlyList<DueTaskDto> dueTasks)
    {
        var expandedEvents = ExpandRecurringEvents(events, day, day);
        var eventsOnDay = expandedEvents.Where(calendarEvent => OccursOnDate(calendarEvent.Details, day)).ToList();
        var allDayEvents = eventsOnDay
            .Where(calendarEvent => calendarEvent.Details.IsAllDay)
            .OrderBy(calendarEvent => calendarEvent.Details.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var timedSlots = eventsOnDay
            .Where(calendarEvent => !calendarEvent.Details.IsAllDay)
            .Select(calendarEvent => BuildTimedSlot(calendarEvent, day))
            .OrderBy(slot => slot.StartMinute)
            .ThenBy(slot => slot.EndMinute)
            .ToList();

        return new DayGrid(day, allDayEvents, AssignColumns(timedSlots), PlaceDueTasksOnTimeline(DueTasksOnDate(dueTasks, day)));
    }

    /// <summary>
    /// Replaces every recurring event in events with one synthetic copy per occurrence that falls within
    /// [windowStartDate, windowEndDateInclusive] (see CalendarEventOccurrenceExpander), so a weekly standup
    /// or monthly bill shows up on each of its actual dates instead of only the single date the event
    /// itself is stored under. Non-recurring events pass through unchanged.
    /// </summary>
    private static IReadOnlyList<CalendarEventDto> ExpandRecurringEvents(
        IReadOnlyList<CalendarEventDto> events, DateOnly windowStartDate, DateOnly windowEndDateInclusive)
    {
        var windowStart = ToLocalStartOfDay(windowStartDate);
        var windowEndExclusive = ToLocalStartOfDay(windowEndDateInclusive.AddDays(1));
        return events
            .SelectMany(calendarEvent => CalendarEventOccurrenceExpander.ExpandOccurrences(calendarEvent, windowStart, windowEndExclusive))
            .ToList();
    }

    private static DateTimeOffset ToLocalStartOfDay(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local));

    private static MonthGridDay BuildMonthGridDay(
        DateOnly date, int displayedMonth, IReadOnlyList<CalendarEventDto> events, IReadOnlyList<DueTaskDto> dueTasks)
    {
        var eventsOnDay = events
            .Where(calendarEvent => OccursOnDate(calendarEvent.Details, date))
            .OrderBy(calendarEvent => calendarEvent.Details.IsAllDay)
            .ThenBy(calendarEvent => calendarEvent.Details.StartUtc.LocalDateTime.TimeOfDay)
            .ToList();
        return new MonthGridDay(date, date.Month == displayedMonth, eventsOnDay, DueTasksOnDate(dueTasks, date));
    }

    /// <summary>Whether details' [StartUtc, EndUtc] range - compared by local calendar date - covers date.</summary>
    private static bool OccursOnDate(CalendarEventDetailsDto details, DateOnly date)
    {
        var startDate = DateOnly.FromDateTime(details.StartUtc.LocalDateTime);
        var endDate = DateOnly.FromDateTime(details.EndUtc.LocalDateTime);
        return date >= startDate && date <= endDate;
    }

    /// <summary>Every dueTasks entry whose local due date falls on date, ordered by time of day like OccursOnDate does for events.</summary>
    private static IReadOnlyList<DueTaskDto> DueTasksOnDate(IReadOnlyList<DueTaskDto> dueTasks, DateOnly date)
        => dueTasks
            .Where(dueTask => DateOnly.FromDateTime(dueTask.DueDateUtc.LocalDateTime) == date)
            .OrderBy(dueTask => dueTask.DueDateUtc.LocalDateTime.TimeOfDay)
            .ToList();

    /// <summary>
    /// Places dueTasksSortedByTime onto the day's minute-precision timeline, the same one timed events are
    /// placed on (see BuildTimedSlot/AssignColumns), so a task with a deadline shows up at its exact due
    /// time instead of in an all-day-like strip at the top of the day.
    /// </summary>
    private static IReadOnlyList<DayGridPlacedDueTask> PlaceDueTasksOnTimeline(IReadOnlyList<DueTaskDto> dueTasksSortedByTime)
    {
        var slots = dueTasksSortedByTime.Select(BuildDueTaskSlot).ToList();
        return AssignDueTaskColumns(slots);
    }

    /// <summary>
    /// A due task has no duration of its own, so its slot is given a fixed marker width (DueTaskMarkerMinutes,
    /// matching the minimum visible size CalendarDayGrid.razor renders any timeline entry at) purely so two
    /// tasks due within a few minutes of each other are still recognized as overlapping and placed in
    /// separate columns instead of being drawn on top of one another.
    /// </summary>
    private static DueTaskTimelineSlot BuildDueTaskSlot(DueTaskDto dueTask)
    {
        var dueMinute = (int)dueTask.DueDateUtc.LocalDateTime.TimeOfDay.TotalMinutes;
        return new DueTaskTimelineSlot(dueTask, dueMinute, dueMinute + DueTaskMarkerMinutes);
    }

    private static int DaysSinceMonday(DayOfWeek dayOfWeek) => ((int)dayOfWeek + 6) % 7;

    private static int DaysUntilSunday(DayOfWeek dayOfWeek) => 6 - DaysSinceMonday(dayOfWeek);

    /// <summary>
    /// Clips calendarEvent's start/end to day's midnight-to-midnight bounds and expresses them as minutes
    /// since local midnight, so a multi-day event only occupies the part of its timeline that actually
    /// falls on this particular day.
    /// </summary>
    private static DayGridTimedSlot BuildTimedSlot(CalendarEventDto calendarEvent, DateOnly day)
    {
        var dayStart = day.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        var localStart = calendarEvent.Details.StartUtc.LocalDateTime;
        var localEnd = calendarEvent.Details.EndUtc.LocalDateTime;
        var clampedStart = localStart < dayStart ? dayStart : localStart;
        var clampedEnd = localEnd > dayEnd ? dayEnd : localEnd;

        var startMinute = (int)(clampedStart - dayStart).TotalMinutes;
        var endMinute = Math.Max(startMinute, (int)(clampedEnd - dayStart).TotalMinutes);
        return new DayGridTimedSlot(calendarEvent, startMinute, endMinute);
    }

    /// <summary>
    /// Splits slotsSortedByStart into clusters of mutually time-overlapping events (a new cluster starts
    /// once a slot begins after every event seen so far has ended) and lays out each cluster independently,
    /// so two events on opposite ends of the day don't end up needlessly sharing columns.
    /// </summary>
    private static IReadOnlyList<DayGridPlacedEvent> AssignColumns(IReadOnlyList<DayGridTimedSlot> slotsSortedByStart)
    {
        var placedEvents = new List<DayGridPlacedEvent>();
        var clusterSlots = new List<DayGridTimedSlot>();
        var clusterEndMinute = 0;

        foreach (var slot in slotsSortedByStart)
        {
            if (clusterSlots.Count > 0 && slot.StartMinute >= clusterEndMinute)
            {
                placedEvents.AddRange(AssignColumnsWithinCluster(clusterSlots));
                clusterSlots.Clear();
            }

            clusterSlots.Add(slot);
            clusterEndMinute = Math.Max(clusterEndMinute, slot.EndMinute);
        }

        if (clusterSlots.Count > 0)
        {
            placedEvents.AddRange(AssignColumnsWithinCluster(clusterSlots));
        }

        return placedEvents;
    }

    /// <summary>
    /// Greedily assigns each event in the cluster the lowest-numbered column whose previous occupant has
    /// already ended, then reports every event in the cluster as sharing the same column count - the
    /// simplest layout that guarantees no two simultaneous events are drawn on top of each other.
    /// </summary>
    private static IEnumerable<DayGridPlacedEvent> AssignColumnsWithinCluster(IReadOnlyList<DayGridTimedSlot> clusterSlots)
    {
        var columnEndMinutes = new List<int>();
        var columnIndexBySlotPosition = new int[clusterSlots.Count];

        for (var slotPosition = 0; slotPosition < clusterSlots.Count; slotPosition++)
        {
            var slot = clusterSlots[slotPosition];
            var columnIndex = columnEndMinutes.FindIndex(columnEndMinute => columnEndMinute <= slot.StartMinute);
            if (columnIndex == -1)
            {
                columnIndex = columnEndMinutes.Count;
                columnEndMinutes.Add(slot.EndMinute);
            }
            else
            {
                columnEndMinutes[columnIndex] = slot.EndMinute;
            }

            columnIndexBySlotPosition[slotPosition] = columnIndex;
        }

        var columnCount = columnEndMinutes.Count;
        return clusterSlots.Select((slot, slotPosition) =>
            new DayGridPlacedEvent(slot.Event, slot.StartMinute, slot.EndMinute, columnIndexBySlotPosition[slotPosition], columnCount));
    }

    private sealed record DayGridTimedSlot(CalendarEventDto Event, int StartMinute, int EndMinute);

    /// <summary>Minutes a due task's marker is treated as occupying on the timeline purely for overlap detection - see BuildDueTaskSlot.</summary>
    private const int DueTaskMarkerMinutes = 15;

    /// <summary>
    /// The due-task equivalent of AssignColumns/AssignColumnsWithinCluster above, kept separate rather than
    /// shared since due tasks are placed by DueTaskTimelineSlot (a point in time plus a synthetic marker
    /// width) rather than by an event's own real start/end range.
    /// </summary>
    private static IReadOnlyList<DayGridPlacedDueTask> AssignDueTaskColumns(IReadOnlyList<DueTaskTimelineSlot> slotsSortedByStart)
    {
        var placedTasks = new List<DayGridPlacedDueTask>();
        var clusterSlots = new List<DueTaskTimelineSlot>();
        var clusterEndMinute = 0;

        foreach (var slot in slotsSortedByStart)
        {
            if (clusterSlots.Count > 0 && slot.DueMinute >= clusterEndMinute)
            {
                placedTasks.AddRange(AssignDueTaskColumnsWithinCluster(clusterSlots));
                clusterSlots.Clear();
            }

            clusterSlots.Add(slot);
            clusterEndMinute = Math.Max(clusterEndMinute, slot.MarkerEndMinute);
        }

        if (clusterSlots.Count > 0)
        {
            placedTasks.AddRange(AssignDueTaskColumnsWithinCluster(clusterSlots));
        }

        return placedTasks;
    }

    private static IEnumerable<DayGridPlacedDueTask> AssignDueTaskColumnsWithinCluster(IReadOnlyList<DueTaskTimelineSlot> clusterSlots)
    {
        var columnEndMinutes = new List<int>();
        var columnIndexBySlotPosition = new int[clusterSlots.Count];

        for (var slotPosition = 0; slotPosition < clusterSlots.Count; slotPosition++)
        {
            var slot = clusterSlots[slotPosition];
            var columnIndex = columnEndMinutes.FindIndex(columnEndMinute => columnEndMinute <= slot.DueMinute);
            if (columnIndex == -1)
            {
                columnIndex = columnEndMinutes.Count;
                columnEndMinutes.Add(slot.MarkerEndMinute);
            }
            else
            {
                columnEndMinutes[columnIndex] = slot.MarkerEndMinute;
            }

            columnIndexBySlotPosition[slotPosition] = columnIndex;
        }

        var columnCount = columnEndMinutes.Count;
        return clusterSlots.Select((slot, slotPosition) =>
            new DayGridPlacedDueTask(slot.Task, slot.DueMinute, columnIndexBySlotPosition[slotPosition], columnCount));
    }

    private sealed record DueTaskTimelineSlot(DueTaskDto Task, int DueMinute, int MarkerEndMinute);
}

/// <summary>One Monday-to-Sunday row of a month grid.</summary>
public sealed record MonthGridWeek(IReadOnlyList<MonthGridDay> Days);

/// <param name="IsInDisplayedMonth">
/// False for a leading/trailing day borrowed from the previous/next month to complete the grid's first or
/// last week - CalendarMonthGrid renders these dimmed rather than omitting them.
/// </param>
public sealed record MonthGridDay(DateOnly Date, bool IsInDisplayedMonth, IReadOnlyList<CalendarEventDto> Events, IReadOnlyList<DueTaskDto> DueTasks);

/// <summary>One month within a year grid, alongside its own month number (1-12) for the month name heading.</summary>
public sealed record YearGridMonth(int Month, IReadOnlyList<MonthGridWeek> Weeks);

/// <summary>
/// A single day's events, split into the all-day strip and the minute-precision timed timeline, plus that
/// same day's due tasks placed onto the timeline alongside the timed events (see DayGridPlacedDueTask).
/// </summary>
public sealed record DayGrid(
    DateOnly Date, IReadOnlyList<CalendarEventDto> AllDayEvents, IReadOnlyList<DayGridPlacedEvent> TimedEvents,
    IReadOnlyList<DayGridPlacedDueTask> DueTasks);

/// <summary>
/// A task item with a due date, placed onto the calendar grid alongside CalendarEventDto entries - see
/// MonthGridDay.DueTasks / DayGrid.DueTasks - so tasks with a deadline show up on the calendar the same
/// way events do, without being calendar events themselves.
/// </summary>
/// <param name="HasPlace">
/// Whether this deadline is somewhere as well as at some time. It changes what clicking it opens: a
/// place to get to opens as its own summary, with a map; everything else opens as the list to tick it
/// off on - see Calendar.razor.
/// </param>
/// <param name="TaskListTitle">
/// The list the item is on. Carried because a calendar shows a day's worth of things from everywhere at
/// once: "Milk" says nothing on its own, while "Shopping: Milk" says where it came from - see
/// <see cref="Label"/>.
/// </param>
public sealed record DueTaskDto(
    Guid TaskListId, Guid TaskItemId, string TaskListTitle, string Description, DateTimeOffset DueDateUtc,
    bool IsCompleted, bool HasPlace = false)
{
    /// <summary>How the entry reads on the calendar: the list it is on, then what it says.</summary>
    public string Label => TaskListTitle.Length == 0 ? Description : $"{TaskListTitle}: {Description}";
}

/// <summary>
/// One timed event's slice of a day's timeline: StartMinute/EndMinute are minutes since local midnight
/// (see CalendarGridBuilder.MinutesPerDay), and ColumnIndex/ColumnCount describe its horizontal slot among
/// any events it overlaps in time - ColumnCount is the same for every event in the same overlap cluster.
/// </summary>
public sealed record DayGridPlacedEvent(CalendarEventDto Event, int StartMinute, int EndMinute, int ColumnIndex, int ColumnCount);

/// <summary>
/// One due task's position on a day's minute-precision timeline: DueMinute is minutes since local midnight
/// (see CalendarGridBuilder.MinutesPerDay), rendered as a small fixed-size marker rather than a start/end
/// block since a task has no duration of its own - ColumnIndex/ColumnCount describe its horizontal slot
/// among any other due tasks it overlaps in time, mirroring DayGridPlacedEvent.
/// </summary>
public sealed record DayGridPlacedDueTask(DueTaskDto Task, int DueMinute, int ColumnIndex, int ColumnCount);
