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
}
