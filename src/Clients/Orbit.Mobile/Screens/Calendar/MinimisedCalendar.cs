namespace Orbit.Mobile.Screens.Calendar;

/// <summary>
/// What is left of the calendar when it gets out of the way. On Android the grid is pinned above the
/// list and minimises to a single row as soon as the reader scrolls past it: a phone has one column and
/// a thumb, so the calendar is either taking the screen or standing aside - see
/// info/future-plan.md, "The calendar that shrinks as you scroll". The web keeps both at once and needs
/// none of this.
///
/// The row that survives is the one the reader is standing on, which is what makes the minimised grid
/// worth keeping at all rather than hiding: a week they can still page through, the month they are in,
/// the hour the day is at.
///
/// Here rather than in the page because it is a rule about which cell matters, not a measurement - the
/// page owns only the scrolling that turns it on.
/// </summary>
public static class MinimisedCalendar
{
    /// <summary>
    /// The week holding the day being looked at: the chosen one, or today when it is in the grid, or
    /// the first week there is. Whole weeks only - a row of four days would read as a broken month.
    /// </summary>
    public static IReadOnlyList<CalendarDay> WeekOf(
        IReadOnlyList<CalendarDay> days, DateTime? selected, DateTime today)
    {
        if (days.Count == 0)
        {
            return days;
        }

        var standingOn = IndexOf(days, selected) ?? IndexOf(days, today.Date) ?? 0;
        return [.. days.Skip(standingOn / DaysInAWeek * DaysInAWeek).Take(DaysInAWeek)];
    }

    /// <summary>
    /// The month being looked at, out of the twelve. The one the grid is showing rather than the one
    /// today is in: paging to next year and finding it still saying "August" would be answering about a
    /// year nobody is reading.
    /// </summary>
    public static IReadOnlyList<CalendarYearMonth> MonthOf(
        IReadOnlyList<CalendarYearMonth> months, DateTime shown)
        => [.. months.Where(month => month.Month.Month == shown.Month)];

    /// <summary>
    /// The one hour of the day worth keeping: the hour it is now, for a day that is today, and the hour
    /// the day's first thing starts in otherwise. Held inside what there is to draw, so an evening's
    /// worth of appointments read at nine in the morning still shows the evening rather than an empty
    /// row above it.
    /// </summary>
    /// <returns>The same shape <see cref="CalendarDayTimeline.HoursWorthDrawing"/> returns.</returns>
    public static (int FirstHour, int LastHour) HourOf(
        IReadOnlyList<DayBlock> blocks, DateTime day, DateTime now)
    {
        var (firstHour, lastHour) = CalendarDayTimeline.HoursWorthDrawing(blocks);
        var hour = day.Date == now.Date ? Math.Clamp(now.Hour, firstHour, lastHour) : firstHour;
        return (hour, hour);
    }

    private const int DaysInAWeek = 7;

    private static int? IndexOf(IReadOnlyList<CalendarDay> days, DateTime? date)
    {
        if (date is not { } wanted)
        {
            return null;
        }

        var found = days.ToList().FindIndex(day => day.Date == wanted.Date);
        return found < 0 ? null : found;
    }
}
