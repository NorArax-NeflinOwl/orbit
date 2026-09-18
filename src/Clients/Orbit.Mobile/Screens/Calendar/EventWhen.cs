using Orbit.Contracts.Calendar;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Calendar;

/// <summary>
/// When an appointment happens, in words - one wording for every screen on this phone that says it.
/// The calendar's own row had the only copy, and the entry an appointment belongs to then said
/// something else about the same appointment: its own due date, which an edit made in a browser never
/// touches. That is what made this its own class, the same way Orbit.Web's EventWhen became one.
///
/// Written in the reader's calendar culture rather than in a fixed order of day and month, which is
/// what every date on this phone does - see Translations.DisplayCulture.
/// </summary>
public static class EventWhen
{
    /// <summary>
    /// The day and the hours it runs between, or the day alone for an all-day one. The end is given as
    /// a time where both ends fall on one day, and in full where they do not - an appointment that runs
    /// past midnight said as "10:00 – 01:00" reads as one that ends nine hours before it starts.
    /// </summary>
    public static string Reads(CalendarEventDetailsDto details, Translations translations)
        => Reads(details.StartUtc, details.EndUtc, details.IsAllDay, translations);

    /// <inheritdoc cref="Reads(CalendarEventDetailsDto, Translations)"/>
    public static string Reads(
        DateTimeOffset startUtc, DateTimeOffset endUtc, bool isAllDay, Translations translations)
    {
        var start = startUtc.LocalDateTime;
        var end = endUtc.LocalDateTime;

        if (isAllDay)
        {
            return translations.Format("{0} · all day", start.ToString("d", translations.DisplayCulture));
        }

        return translations.Format(
            "{0} – {1}",
            start.ToString("g", translations.DisplayCulture),
            start.Date == end.Date
                ? end.ToString("t", translations.DisplayCulture)
                : end.ToString("g", translations.DisplayCulture));
    }
}
