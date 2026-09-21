using System.Globalization;
using Orbit.Core.Notifications;

namespace Orbit.Core.Calendar.Reminders;

/// <summary>
/// Builds the push notification payload for a due calendar event reminder - the push counterpart of
/// <see cref="EventReminderEmailContent"/>, sent alongside the email by
/// CalendarEventReminderBackgroundService.
/// </summary>
public static class EventReminderPushContent
{
    public static PushNotificationPayload Build(CalendarEventDetails details, Guid calendarEventId, int minutesBeforeStart)
    {
        // The lead time is part of the sentence rather than an argument, because how long "in 2 hr"
        // is depends on the language it is said in - Polish does not put a number in front of a noun
        // and leave it there. Three sentences, each its own key, is what lets a translator write three
        // real sentences instead of gluing one out of pieces.
        return minutesBeforeStart switch
        {
            0 => Payload("The event \"{0}\" is starting now."),
            _ when minutesBeforeStart % 60 == 0 => Payload(
                "The event \"{0}\" starts in {1} hr.", (minutesBeforeStart / 60).ToString(CultureInfo.InvariantCulture)),
            _ => Payload(
                "The event \"{0}\" starts in {1} min.", minutesBeforeStart.ToString(CultureInfo.InvariantCulture))
        };

        PushNotificationPayload Payload(string bodyFormat, string? leadTime = null)
            => new(
                "Upcoming event", bodyFormat,
                leadTime is null ? [details.Title] : [details.Title, leadTime],
                $"/calendar/{calendarEventId}");
    }

    /// <summary>
    /// One reminder for everything of a reader's that fell due in the same poll - two appointments at
    /// nine cost the reader the second banner otherwise, for the reason Orbit.Core.Tasks.SeveralEntriesAtOnce
    /// gives. A single reminder says what it always said.
    ///
    /// The events are named and nothing else. Their lead times can differ, and a start time would have
    /// to be written in the reader's own time zone, which the server does not know - the calendar the
    /// notice leads to says both. It leads to the calendar rather than to one of them, since there is no
    /// page for "these two".
    /// </summary>
    public static PushNotificationPayload Build(IReadOnlyList<EventReminderOccurrence> reminders)
        => reminders is [var theOnlyOne]
            ? Build(theOnlyOne.Details, theOnlyOne.CalendarEventId, theOnlyOne.MinutesBeforeStart)
            : new PushNotificationPayload(
                "Upcoming events", "These events are coming up: {0}.",
                [string.Join(", ", reminders.Select(reminder => reminder.Details.Title))],
                "/calendar");
}
