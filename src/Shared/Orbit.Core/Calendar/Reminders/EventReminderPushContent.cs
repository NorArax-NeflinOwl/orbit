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
        var body = $"Wydarzenie \"{details.Title}\" zaczyna się {FormatLeadTime(minutesBeforeStart)}.";
        return new PushNotificationPayload("Zbliżające się wydarzenie", body, $"/calendar/{calendarEventId}");
    }

    private static string FormatLeadTime(int minutesBeforeStart)
        => minutesBeforeStart switch
        {
            0 => "teraz",
            _ when minutesBeforeStart % 60 == 0 => $"za {minutesBeforeStart / 60} godz.",
            _ => $"za {minutesBeforeStart} min"
        };
}
