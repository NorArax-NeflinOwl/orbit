using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Calendar;

/// <summary>
/// One cell of the month grid: which day it is, whether it belongs to the month being shown, how many
/// events fall on it, and whether it is the one being looked at.
/// </summary>
/// <param name="IsInMonth">
/// False for the days either side that fill the first and last weeks. They are still shown - a grid
/// with holes in it is harder to read than one with quiet edges - but they are quiet.
/// </param>
public sealed record CalendarDay(
    DateTime Date, bool IsInMonth, bool IsToday, bool IsSelected, int EventCount)
{
    public string DayNumber => Date.Day.ToString();

    /// <summary>
    /// The whole date said out loud, and how much is on it. A cell shows a number and a dot, which is
    /// enough to see and nothing to hear: "5" alone says neither which month it is in nor that anything
    /// is happening. Set by CalendarViewModel, which has the language.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    public bool HasEvents => EventCount > 0;
}

/// <summary>
/// The month grid the phone did not have: six weeks of seven days, whatever month it is, so the grid
/// never changes height as the reader pages through it.
///
/// Orbit.Web offers day, month and year. A phone gets the month, which is the one that answers "what is
/// this month like" at a glance - the other two are a list of one day and a list of twelve months, and
/// the list underneath already is the first of those.
/// </summary>
public static class CalendarMonth
{
    private const int WeeksShown = 6;

    /// <param name="deadlines">
    /// Counted with the events, because a day with something due on it is not an empty day - see
    /// <see cref="CalendarDeadline"/>.
    /// </param>
    public static IReadOnlyList<CalendarDay> Build(
        DateTime month, DateTime? selected, DateTime today, IReadOnlyList<LocalCalendarEvent> events,
        IReadOnlyList<CalendarDeadline> deadlines)
    {
        var counts = events
            .Select(calendarEvent => calendarEvent.Details.StartUtc.ToLocalTime().Date)
            .Concat(deadlines.Select(deadline => deadline.DueLocalDate))
            .GroupBy(date => date)
            .ToDictionary(day => day.Key, day => day.Count());

        var firstOfMonth = new DateTime(month.Year, month.Month, 1);

        // Monday first, as a Polish and a British calendar both read - see DayOfWeek, where Sunday is 0.
        var offset = ((int)firstOfMonth.DayOfWeek + 6) % 7;
        var firstCell = firstOfMonth.AddDays(-offset);

        return [.. Enumerable.Range(0, WeeksShown * 7)
            .Select(index => firstCell.AddDays(index))
            .Select(date => new CalendarDay(
                date,
                date.Month == firstOfMonth.Month && date.Year == firstOfMonth.Year,
                date == today.Date,
                selected is { } chosen && date == chosen.Date,
                counts.GetValueOrDefault(date)))];
    }

    /// <summary>The weekday initials across the top, in the reader's own calendar and starting Monday.</summary>
    public static IReadOnlyList<string> WeekdayNames(Translations translations)
        => [.. Enumerable.Range(0, 7)
            .Select(index => new DateTime(2026, 1, 5).AddDays(index))
            .Select(date => date.ToString("ddd", translations.DisplayCulture))];

    /// <summary>"August 2026", in the reader's own calendar.</summary>
    public static string Describe(DateTime month, Translations translations)
        => month.ToString("MMMM yyyy", translations.DisplayCulture);
}
