using System.Globalization;
using System.Web;

namespace Orbit.Web.Services;

/// <summary>
/// Builds a Google Calendar "add this event" link: an ordinary URL that opens Google Calendar with the
/// event already filled in, for the user to save into whichever of their calendars they choose.
///
/// Deliberately a link rather than a call to Google's Calendar API. Writing into someone's calendar
/// server-side would need an OAuth consent flow, a client secret, stored refresh tokens, and Google's
/// review of a sensitive scope - none of which Orbit has (see GoogleAuthSettings: sign-in only). A link
/// reaches the same end, the user stays in control of what actually lands in their calendar, and it
/// works the moment they click it.
/// </summary>
public static class GoogleCalendarEventLink
{
    private const string TemplateUrl = "https://calendar.google.com/calendar/render?action=TEMPLATE";

    /// <summary>
    /// A link for an event running between two instants. <paramref name="isAllDay"/> switches to Google's
    /// date-only form, whose end is exclusive - an all-day event on the 1st is written as 1st/2nd.
    /// </summary>
    public static string ForEvent(
        string title, DateTimeOffset startUtc, DateTimeOffset endUtc, bool isAllDay = false,
        string? description = null, string? location = null)
    {
        var dates = isAllDay
            ? $"{FormatDate(startUtc)}/{FormatDate(endUtc.Date > startUtc.Date ? endUtc : endUtc.AddDays(1))}"
            : $"{FormatInstant(startUtc)}/{FormatInstant(endUtc)}";

        var url = $"{TemplateUrl}&text={Encode(title)}&dates={dates}";
        if (!string.IsNullOrWhiteSpace(description))
        {
            url += $"&details={Encode(description)}";
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            url += $"&location={Encode(location)}";
        }

        return url;
    }

    /// <summary>
    /// A link for a task item that has a due date, as a short event ending at that moment - Google
    /// Calendar has no "task" its template links can create, and an event at the deadline is what a
    /// reminder in a calendar actually looks like.
    /// </summary>
    public static string ForTaskItem(string description, DateTimeOffset dueDateUtc, string? taskListTitle = null)
        => ForEvent(
            description,
            dueDateUtc.AddMinutes(-DefaultTaskDurationMinutes),
            dueDateUtc,
            isAllDay: false,
            description: taskListTitle is null ? null : $"From your Orbit task list \"{taskListTitle}\".");

    /// <summary>How long the event standing in for a task runs - long enough to be visible in a day view, short enough not to block out time the task doesn't need.</summary>
    private const int DefaultTaskDurationMinutes = 30;

    /// <summary>Google's template links expect UTC in this exact shape; anything else is silently ignored.</summary>
    private static string FormatInstant(DateTimeOffset instant)
        => instant.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string FormatDate(DateTimeOffset instant)
        => instant.ToUniversalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    private static string Encode(string value) => HttpUtility.UrlEncode(value);
}
