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
    public static IReadOnlyList<MonthGridWeek> BuildMonthGrid(DateOnly monthReferenceDate, IReadOnlyList<CalendarEventDto> events)
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
                .Select(dayOffset => BuildMonthGridDay(weekStart.AddDays(dayOffset), monthReferenceDate.Month, expandedEvents))
                .ToList();
            weeks.Add(new MonthGridWeek(days));
        }

        return weeks;
    }

    /// <summary>Builds all 12 of a year's month grids in one call, for the year view.</summary>
    public static IReadOnlyList<YearGridMonth> BuildYearGrid(int year, IReadOnlyList<CalendarEventDto> events)
        => Enumerable.Range(1, 12)
            .Select(month => new YearGridMonth(month, BuildMonthGrid(new DateOnly(year, month, 1), events)))
            .ToList();

    /// <summary>
    /// Splits day's events into an all-day strip and a set of timed events placed on a minute-precision
    /// timeline (see DayGridPlacedEvent), with simultaneous events assigned side-by-side columns instead
    /// of overlapping each other.
    /// </summary>
    public static DayGrid BuildDayGrid(DateOnly day, IReadOnlyList<CalendarEventDto> events)
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

        return new DayGrid(day, allDayEvents, AssignColumns(timedSlots));
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

    private static MonthGridDay BuildMonthGridDay(DateOnly date, int displayedMonth, IReadOnlyList<CalendarEventDto> events)
    {
        var eventsOnDay = events
            .Where(calendarEvent => OccursOnDate(calendarEvent.Details, date))
            .OrderBy(calendarEvent => calendarEvent.Details.IsAllDay)
            .ThenBy(calendarEvent => calendarEvent.Details.StartUtc.LocalDateTime.TimeOfDay)
            .ToList();
        return new MonthGridDay(date, date.Month == displayedMonth, eventsOnDay);
    }

    /// <summary>Whether details' [StartUtc, EndUtc] range - compared by local calendar date - covers date.</summary>
    private static bool OccursOnDate(CalendarEventDetailsDto details, DateOnly date)
    {
        var startDate = DateOnly.FromDateTime(details.StartUtc.LocalDateTime);
        var endDate = DateOnly.FromDateTime(details.EndUtc.LocalDateTime);
        return date >= startDate && date <= endDate;
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
}

/// <summary>One Monday-to-Sunday row of a month grid.</summary>
public sealed record MonthGridWeek(IReadOnlyList<MonthGridDay> Days);

/// <param name="IsInDisplayedMonth">
/// False for a leading/trailing day borrowed from the previous/next month to complete the grid's first or
/// last week - CalendarMonthGrid renders these dimmed rather than omitting them.
/// </param>
public sealed record MonthGridDay(DateOnly Date, bool IsInDisplayedMonth, IReadOnlyList<CalendarEventDto> Events);

/// <summary>One month within a year grid, alongside its own month number (1-12) for the month name heading.</summary>
public sealed record YearGridMonth(int Month, IReadOnlyList<MonthGridWeek> Weeks);

/// <summary>A single day's events, split into the all-day strip and the minute-precision timed timeline.</summary>
public sealed record DayGrid(DateOnly Date, IReadOnlyList<CalendarEventDto> AllDayEvents, IReadOnlyList<DayGridPlacedEvent> TimedEvents);

/// <summary>
/// One timed event's slice of a day's timeline: StartMinute/EndMinute are minutes since local midnight
/// (see CalendarGridBuilder.MinutesPerDay), and ColumnIndex/ColumnCount describe its horizontal slot among
/// any events it overlaps in time - ColumnCount is the same for every event in the same overlap cluster.
/// </summary>
public sealed record DayGridPlacedEvent(CalendarEventDto Event, int StartMinute, int EndMinute, int ColumnIndex, int ColumnCount);
