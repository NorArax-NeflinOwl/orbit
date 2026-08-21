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
        var body = $"Wydarzenie \"{details.Title}\" zostało zapisane w kalendarzu.";
        return new PushNotificationPayload("Utworzono wydarzenie", body, $"/calendar/{calendarEventId}");
    }
}
