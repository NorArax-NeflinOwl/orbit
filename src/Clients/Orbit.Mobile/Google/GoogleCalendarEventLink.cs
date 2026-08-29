using System.Globalization;
using System.Web;
using Orbit.Contracts.Calendar;

namespace Orbit.Mobile.Google;

/// <summary>
/// Builds a Google Calendar "add this event" link: an ordinary URL that opens Google Calendar with the
/// event already filled in, for the reader to save into whichever of their calendars they choose.
///
/// Deliberately a link rather than a call to Google's Calendar API. Writing into someone's calendar
/// would need an OAuth consent flow, a client secret, stored refresh tokens, and Google's review of a
/// sensitive scope - none of which Orbit has (see GoogleAuthSettings: sign-in only). A link reaches the
/// same end, the reader stays in control of what actually lands in their calendar, and it works the
/// moment they tap it.
///
/// The phone's own twin of Orbit.Web's GoogleCalendarEventLink - see GoogleMapsLink for why each client
/// carries its own.
/// </summary>
public static class GoogleCalendarEventLink
{
    private const string TemplateUrl = "https://calendar.google.com/calendar/render?action=TEMPLATE";

    /// <summary>How long the event standing in for a task runs - long enough to be visible in a day view, short enough not to block out time the task doesn't need.</summary>
    private const int DefaultTaskDurationMinutes = 30;

    /// <summary>
    /// A link for an event running between two instants. <paramref name="isAllDay"/> switches to Google's
    /// date-only form, whose end is exclusive - an all-day event on the 1st is written as 1st/2nd.
    /// </summary>
    public static string ForEvent(
        string title, DateTimeOffset startUtc, DateTimeOffset endUtc, bool isAllDay = false,
        string? description = null, string? location = null, RecurrenceDto? recurrence = null)
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

        if (ToRecurrenceRule(recurrence) is { } rule)
        {
            url += $"&recur={Encode(rule)}";
        }

        // Guests are deliberately left out. Google's template links take them as "&add=<address>", which
        // would put other people's email addresses into a URL - in this reader's history, in whatever
        // logs it passes through - to save them a step they can do in Google's own form.
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

    /// <summary>
    /// Orbit's recurrence as the iCalendar rule Google's template links read. Null for an event that
    /// does not repeat, and for a frequency this version does not know - an unrecognised rule makes
    /// Google drop the whole link's recurrence silently, and one occurrence is a better wrong answer
    /// than a link that opens an empty form.
    /// </summary>
    private static string? ToRecurrenceRule(RecurrenceDto? recurrence)
    {
        if (recurrence is null)
        {
            return null;
        }

        var frequency = recurrence.Frequency switch
        {
            nameof(RecurrenceFrequencyNames.Daily) => "DAILY",
            nameof(RecurrenceFrequencyNames.Weekly) => "WEEKLY",
            nameof(RecurrenceFrequencyNames.Monthly) => "MONTHLY",
            _ => null
        };
        if (frequency is null)
        {
            return null;
        }

        var rule = $"RRULE:FREQ={frequency}";
        // INTERVAL=1 is the default, so saying it adds length and nothing else.
        if (recurrence.IntervalCount > 1)
        {
            rule += $";INTERVAL={recurrence.IntervalCount.ToString(CultureInfo.InvariantCulture)}";
        }

        if (recurrence.UntilUtc is { } until)
        {
            rule += $";UNTIL={FormatInstant(until)}";
        }

        return rule;
    }

    /// <summary>
    /// The frequency names as they travel in RecurrenceDto.Frequency - see Orbit.Core's
    /// RecurrenceFrequency, which the API serialises by name.
    /// </summary>
    private enum RecurrenceFrequencyNames
    {
        Daily,
        Weekly,
        Monthly
    }

    /// <summary>Google's template links expect UTC in this exact shape; anything else is silently ignored.</summary>
    private static string FormatInstant(DateTimeOffset instant)
        => instant.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string FormatDate(DateTimeOffset instant)
        => instant.ToUniversalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    private static string Encode(string value) => HttpUtility.UrlEncode(value);
}
