namespace Orbit.Core.Tasks.DailyReminders;

/// <summary>Builds the subject and body of a task item's daily "remind daily" reminder e-mail.</summary>
public static class DailyTaskReminderEmailContent
{
    public static (string Subject, string Body) Build(DueDailyTaskReminder reminder)
    {
        var subject = $"Reminder: {reminder.Description}";

        var bodyLines = new List<string>
        {
            $"Task \"{reminder.Description}\" from list \"{reminder.TaskListTitle}\" is still waiting to be done."
        };

        if (reminder.DueDateUtc is { } dueDateUtc)
        {
            bodyLines.Add($"Due: {dueDateUtc.LocalDateTime:dd.MM.yyyy HH:mm}");
        }

        return (subject, string.Join(Environment.NewLine, bodyLines));
    }
}
