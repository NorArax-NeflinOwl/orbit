using Orbit.Contracts.Calendar;

namespace Orbit.Web.Services;

/// <summary>
/// When an appointment happens, in words - one wording for every screen that says it. The calendar's
/// list had the only copy, and the entry's own page then said something different about the same
/// appointment; the day it said nothing at all is what made this its own class.
/// </summary>
public static class EventWhen
{
    /// <summary>
    /// "15.03.2026 10:00 – 11:00" for the ordinary case, the day on its own for an all-day one, and both
    /// ends spelled out when it runs past midnight. Times are the reader's own, since an appointment is
    /// somewhere they have to be.
    /// </summary>
    public static string Reads(CalendarEventDetailsDto details, Translations translations)
    {
        var start = details.StartUtc.LocalDateTime;
        var end = details.EndUtc.LocalDateTime;

        if (details.IsAllDay)
        {
            return start.Date == end.Date
                ? translations.Format("{0} (all day)", start.ToString("dd.MM.yyyy"))
                : $"{start:dd.MM.yyyy} – {end:dd.MM.yyyy}";
        }

        return start.Date == end.Date
            ? $"{start:dd.MM.yyyy} {start:HH:mm} – {end:HH:mm}"
            : $"{start:dd.MM.yyyy HH:mm} – {end:dd.MM.yyyy HH:mm}";
    }
}
