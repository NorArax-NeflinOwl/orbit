namespace Orbit.Mobile.Screens.Calendar;

/// <summary>
/// Which row of the month grid one week is.
///
/// The week view draws the same cells the month does, one row of them - which is what makes stepping
/// between the two read as one calendar rather than as two screens that happen to share a colour. So
/// the week is picked out of the grid that was already built rather than built again from the dates.
///
/// It was <c>MinimisedCalendar</c>: the grid used to shrink to this row on its own as the list beneath
/// it was scrolled past, and grow back at the top. The week is one of the four views the reader asks
/// for by name now, so nothing shrinks by itself and the two other things that shrank - the year to one
/// month, the day to one hour - are gone with the gesture that caused them.
/// </summary>
public static class CalendarWeek
{
    /// <summary>
    /// The week holding the day being looked at: the chosen one, or today when it is in the grid, or
    /// the first week there is. Whole weeks only - a row of four days would read as a broken month.
    /// </summary>
    public static IReadOnlyList<CalendarDay> Of(
        IReadOnlyList<CalendarDay> days, DateTime? shown, DateTime today)
    {
        if (days.Count == 0)
        {
            return days;
        }

        var standingOn = IndexOf(days, shown) ?? IndexOf(days, today.Date) ?? 0;
        return [.. days.Skip(standingOn / DaysInAWeek * DaysInAWeek).Take(DaysInAWeek)];
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
