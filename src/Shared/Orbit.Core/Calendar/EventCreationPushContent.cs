using Orbit.Core.Notifications;

namespace Orbit.Core.Calendar;

/// <summary>
/// Builds the push notification payload sent once, immediately, when a calendar event is first created
/// with <see cref="CalendarEventDetails.CreationNotificationChannel"/> including
/// <see cref="NotificationChannel.Push"/> - the push counterpart of <see cref="EventCreationEmailContent"/>.
/// </summary>
public static class EventCreationPushContent
{
    public static PushNotificationPayload Build(CalendarEventDetails details, Guid calendarEventId)
    {
        return new PushNotificationPayload(
            "Event created", "The event \"{0}\" has been saved to your calendar.", [details.Title],
            $"/calendar/{calendarEventId}");
    }
}
