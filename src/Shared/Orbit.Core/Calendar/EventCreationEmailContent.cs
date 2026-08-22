namespace Orbit.Core.Calendar;

/// <summary>
/// Builds the subject and body of the email sent once, immediately, when a calendar event is first
/// created with <see cref="CalendarEventDetails.CreationNotificationChannel"/> including
/// <see cref="Notifications.NotificationChannel.Email"/> - see
/// <see cref="Reminders.EventReminderEmailContent"/> for the separate "event is approaching" email.
/// </summary>
public static class EventCreationEmailContent
{
    public static (string Subject, string Body) Build(CalendarEventDetails details)
    {
        var subject = $"Event created: {details.Title}";

        var startLabel = details.IsAllDay
            ? $"{details.StartUtc.LocalDateTime:dd.MM.yyyy} (all day)"
            : $"{details.StartUtc.LocalDateTime:dd.MM.yyyy HH:mm}";

        var bodyLines = new List<string>
        {
            $"The event \"{details.Title}\" has been saved to your calendar.",
            $"Start: {startLabel}"
        };

        if (!string.IsNullOrWhiteSpace(details.Description))
        {
            bodyLines.Add($"Description: {details.Description}");
        }

        return (subject, string.Join(Environment.NewLine, bodyLines));
    }
}
