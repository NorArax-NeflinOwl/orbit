using System.Globalization;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Calendar;

/// <summary>
/// One month of the year overview: its name, how much is in it, and whether it is the month we are in.
/// </summary>
public sealed record CalendarYearMonth(DateTime Month, string Name, int EventCount, bool IsThisMonth)
{
    public bool HasEvents => EventCount > 0;
}

/// <summary>
/// The year at a glance: twelve months, each with what it holds.
///
/// Orbit.Web draws twelve day grids here. A phone is not wide enough for that - a day cell would end up
/// smaller than a fingertip - so the year answers the same question a different way: which months have
/// anything in them, and take me to one.
/// </summary>
public static class CalendarYear
{
    private const int MonthsInYear = 12;

    public static IReadOnlyList<CalendarYearMonth> Build(
        int year, DateTime today, IReadOnlyList<LocalCalendarEvent> events, Translations translations)
    {
        var counts = events
            .Select(calendarEvent => calendarEvent.Details.StartUtc.ToLocalTime())
            .Where(start => start.Year == year)
            .GroupBy(start => start.Month)
            .ToDictionary(month => month.Key, month => month.Count());

        return [.. Enumerable.Range(1, MonthsInYear)
            .Select(month => new DateTime(year, month, 1))
            .Select(month => new CalendarYearMonth(
                month,
                Name(month, translations),
                counts.GetValueOrDefault(month.Month),
                month.Month == today.Month && month.Year == today.Year))];
    }

    /// <summary>"2026" - a year reads the same in every calendar this client offers.</summary>
    public static string Describe(int year) => year.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Polish writes month names lower-case; a grid of headings reads better with each capitalised, the
    /// same as the English ones already are. Orbit.Web capitalises them here for the same reason.
    /// </summary>
    private static string Name(DateTime month, Translations translations)
    {
        var name = month.ToString("MMMM", translations.DisplayCulture);
        return name.Length == 0 ? name : char.ToUpper(name[0], translations.DisplayCulture) + name[1..];
    }
}
