namespace Orbit.Core.Calendar;

/// <summary>
/// Builds the subject and body of the email sent once, immediately, when a calendar event is first
/// created with <see cref="CalendarEventDetails.NotifyOnCreation"/> turned on - see
/// <see cref="Reminders.EventReminderEmailContent"/> for the separate "event is approaching" email.
/// </summary>
public static class EventCreationEmailContent
{
    public static (string Subject, string Body) Build(CalendarEventDetails details)
    {
        var subject = $"Utworzono wydarzenie: {details.Title}";

        var startLabel = details.IsAllDay
            ? $"{details.StartUtc.LocalDateTime:dd.MM.yyyy} (cały dzień)"
            : $"{details.StartUtc.LocalDateTime:dd.MM.yyyy HH:mm}";

        var bodyLines = new List<string>
        {
            $"Wydarzenie \"{details.Title}\" zostało zapisane w kalendarzu.",
            $"Początek: {startLabel}"
        };

        if (!string.IsNullOrWhiteSpace(details.Description))
        {
            bodyLines.Add($"Opis: {details.Description}");
        }

        return (subject, string.Join(Environment.NewLine, bodyLines));
    }
}
