using System.Globalization;
using System.Web;
using Orbit.Contracts.Calendar;

namespace Orbit.Mobile.Google;

/// <summary>
/// One event as Google's template links can carry it. Grouped rather than passed as a dozen arguments:
/// these all describe the same event, and only the first three are ever certainly there.
/// </summary>
/// <param name="Title">
/// Orbit's own title, newlines and all - a pasted name can hold more than one line. Google's title is a
/// single line, so only the first survives as one; see GoogleCalendarEventLink.ForEvent.
/// </param>
public sealed record GoogleCalendarEvent(string Title, DateTimeOffset StartUtc, DateTimeOffset EndUtc)
{
    public bool IsAllDay { get; init; }

    public string? Description { get; init; }

    /// <summary>What Google should show as the place - an address, or coordinates when there is no address.</summary>
    public string? Location { get; init; }

    public RecurrenceDto? Recurrence { get; init; }

    /// <summary>
    /// The list this appointment belongs to, when a task list made it - see
    /// LinkCalendarEventToTaskListCommand. Google gets it in front of the event's own name, because in
    /// somebody else's calendar "Dentist" alone does not say which of their lists it came from.
    /// </summary>
    public string? TaskListTitle { get; init; }

    /// <summary>
    /// The guests' addresses, already narrowed to the ones Google itself has verified - see
    /// ContactDto.HasGoogleVerifiedEmail. An address Google does not know is an invitation that bounces.
    /// </summary>
    public IReadOnlyList<string> GuestEmailAddresses { get; init; } = [];

    /// <summary>Orbit's lead times, written into the description - see GoogleCalendarEventLink.RemindersLine.</summary>
    public IReadOnlyList<int> ReminderMinutesBeforeStart { get; init; } = [];

    public bool NotifyAtStart { get; init; }
}

/// <summary>
/// Builds a Google Calendar "add this event" link: an ordinary URL that opens Google Calendar with the
/// event already filled in, for the user to save into whichever of their calendars they choose.
///
/// Deliberately a link rather than a call to Google's Calendar API. Writing into someone's calendar
/// would need an OAuth consent flow, a client secret, stored refresh tokens, and Google's review of a
/// sensitive scope - none of which Orbit has (see GoogleAuthSettings: sign-in only). A link reaches the
/// same end, the reader stays in control of what actually lands in their calendar, and it works the
/// moment they tap it.
///
/// The phone's own twin of Orbit.Web's GoogleCalendarEventLink - see GoogleMapsLink for why each client
/// carries its own, and the tests on both sides for what keeps the two saying the same thing. They had
/// come apart: this one still wrote a multi-day event a day short, and knew nothing of a title's second
/// line, the list an appointment came from, its guests or its reminders.
///
/// What that costs: a template link takes a title, dates, a description, a place, a rule and guests, and
/// nothing else. It has no parameter for an event's colour and none for its reminders, so neither can be
/// handed over as such - the lead times are written into the description instead, where they at least
/// travel as words. Both would need the API above.
/// </summary>
public static class GoogleCalendarEventLink
{
    private const string TemplateUrl = "https://calendar.google.com/calendar/render?action=TEMPLATE";

    /// <summary>
    /// A link for an event running between two instants. <see cref="GoogleCalendarEvent.IsAllDay"/>
    /// switches to Google's date-only form, whose end is exclusive - an all-day event on the 1st is
    /// written as 1st/2nd.
    /// </summary>
    public static string ForEvent(GoogleCalendarEvent theEvent)
    {
        // Which day an all-day event falls on is read in the reader's own zone, not in UTC. An all-day
        // event is stored as the instant local midnight began (see EventFormModel.ToDateTimeOffset), so
        // anywhere east of Greenwich that instant belongs to the previous UTC day - and a holiday on the
        // 14th was handed to Google as the 13th. A timed event has no such problem: it goes as an
        // instant, and the Z tells Google exactly which one.
        //
        // The day after the last one, always. Orbit's own end date is the last day the event covers -
        // that is what the calendar draws, see CalendarGridBuilder.OccursOnDate, which includes it - and
        // Google's is the first day it does not. A trip from the 14th to the 16th is three days in the
        // grid and has to be three in the link: 14th to the 17th. Passing a multi-day end through
        // unchanged, as this did, made every such event a day short of what the grid showed.
        var dates = theEvent.IsAllDay
            ? $"{FormatDate(theEvent.StartUtc)}/{FormatDate(theEvent.EndUtc.AddDays(1))}"
            : $"{FormatInstant(theEvent.StartUtc)}/{FormatInstant(theEvent.EndUtc)}";

        var url = $"{TemplateUrl}&text={Encode(TitleFor(theEvent))}&dates={dates}";
        if (DescriptionFor(theEvent) is { } description)
        {
            url += $"&details={Encode(description)}";
        }

        if (!string.IsNullOrWhiteSpace(theEvent.Location))
        {
            url += $"&location={Encode(theEvent.Location)}";
        }

        if (ToRecurrenceRule(theEvent.Recurrence) is { } rule)
        {
            url += $"&recur={Encode(rule)}";
        }

        // One "add" per guest is how Google's template links take them. Their access level cannot come
        // along: Google has no per-guest role in a template link - everyone arrives as an ordinary
        // guest - so an Orbit guest who may edit but not share arrives as read-only, which is the
        // narrower of the two and the safe way to be wrong.
        foreach (var emailAddress in theEvent.GuestEmailAddresses)
        {
            url += $"&add={Encode(emailAddress)}";
        }

        return url;
    }

    /// <summary>
    /// The event's name as Google can hold it: one line, prefixed with the task list that raised this
    /// appointment when one did.
    /// </summary>
    private static string TitleFor(GoogleCalendarEvent theEvent)
    {
        var name = TitleLines(theEvent)[0];
        return string.IsNullOrWhiteSpace(theEvent.TaskListTitle) ? name : $"{theEvent.TaskListTitle} - {name}";
    }

    /// <summary>
    /// Everything Google's description should say: the lines of the title it had no room for, then the
    /// description itself, then the reminders Orbit would have given - which a template link has no
    /// field for, so they go as words or not at all. Null when all three are empty, so an event with
    /// nothing to say carries no empty parameter.
    /// </summary>
    private static string? DescriptionFor(GoogleCalendarEvent theEvent)
    {
        var paragraphs = new List<string>();

        var titleOverflow = string.Join(Environment.NewLine, TitleLines(theEvent).Skip(1)).Trim();
        if (titleOverflow.Length > 0)
        {
            paragraphs.Add(titleOverflow);
        }

        if (!string.IsNullOrWhiteSpace(theEvent.Description))
        {
            paragraphs.Add(theEvent.Description);
        }

        if (RemindersLine(theEvent) is { } reminders)
        {
            paragraphs.Add(reminders);
        }

        return paragraphs.Count == 0 ? null : string.Join(Environment.NewLine, paragraphs);
    }

    /// <summary>
    /// The title split into lines. Never empty - a split always yields at least one piece - so the
    /// first line can be read without a guard even for an event whose name is blank.
    /// </summary>
    private static IReadOnlyList<string> TitleLines(GoogleCalendarEvent theEvent)
        => [.. theEvent.Title.Split('\n').Select(line => line.TrimEnd('\r'))];

    /// <summary>
    /// Orbit's reminders said in words. Google's own notifications on the saved event are whatever that
    /// calendar's defaults are - a template link cannot set them - so this is what carries the fact that
    /// the reader asked to be told a day before at all.
    /// </summary>
    private static string? RemindersLine(GoogleCalendarEvent theEvent)
    {
        var leadTimes = theEvent.ReminderMinutesBeforeStart
            .Where(minutes => minutes > 0)
            .Select(FormatLeadTime)
            .ToList();
        if (theEvent.NotifyAtStart || theEvent.ReminderMinutesBeforeStart.Contains(0))
        {
            leadTimes.Add("at the start");
        }

        return leadTimes.Count == 0 ? null : $"Orbit reminders: {string.Join(", ", leadTimes)}.";
    }

    private static string FormatLeadTime(int minutesBeforeStart)
        => minutesBeforeStart switch
        {
            _ when minutesBeforeStart % (24 * 60) == 0 => $"{minutesBeforeStart / (24 * 60)} days before",
            _ when minutesBeforeStart % 60 == 0 => $"{minutesBeforeStart / 60} hr before",
            _ => $"{minutesBeforeStart} min before"
        };

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

    /// <summary>
    /// A link for a task item that has a due date, as a short event ending at that moment - Google
    /// Calendar has no "task" its template links can create, and an event at the deadline is what a
    /// reminder in a calendar actually looks like.
    /// </summary>
    public static string ForTaskItem(string description, DateTimeOffset dueDateUtc, string? taskListTitle = null)
        => ForEvent(new GoogleCalendarEvent(description, dueDateUtc.AddMinutes(-DefaultTaskDurationMinutes), dueDateUtc)
        {
            Description = taskListTitle is null ? null : $"From your Orbit task list \"{taskListTitle}\"."
        });

    /// <summary>How long the event standing in for a task runs - long enough to be visible in a day view, short enough not to block out time the task doesn't need.</summary>
    private const int DefaultTaskDurationMinutes = 30;

    /// <summary>Google's template links expect UTC in this exact shape; anything else is silently ignored.</summary>
    private static string FormatInstant(DateTimeOffset instant)
        => instant.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// The day an instant falls on where the reader is. Google's date-only form carries no zone, so what
    /// it means is "this calendar day" - and the day has to be the one the reader picked rather than the
    /// one the instant lands on in UTC.
    /// </summary>
    private static string FormatDate(DateTimeOffset instant)
        => instant.ToLocalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    private static string Encode(string value) => HttpUtility.UrlEncode(value);
}
