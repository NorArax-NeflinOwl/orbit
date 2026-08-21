namespace Orbit.Core.Tasks.DailyReminders;

/// <summary>Builds the subject and body of a task item's daily "remind daily" reminder e-mail.</summary>
public static class DailyTaskReminderEmailContent
{
    public static (string Subject, string Body) Build(DueDailyTaskReminder reminder)
    {
        var subject = $"Przypomnienie: {reminder.Description}";

        var bodyLines = new List<string>
        {
            $"Zadanie \"{reminder.Description}\" z listy \"{reminder.TaskListTitle}\" wciąż czeka na wykonanie."
        };

        if (reminder.DueDateUtc is { } dueDateUtc)
        {
            bodyLines.Add($"Termin: {dueDateUtc.LocalDateTime:dd.MM.yyyy HH:mm}");
        }

        return (subject, string.Join(Environment.NewLine, bodyLines));
    }
}
