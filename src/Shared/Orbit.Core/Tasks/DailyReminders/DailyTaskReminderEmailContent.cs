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

    /// <summary>
    /// One e-mail for everything that came due in the same poll, a line per entry - the same gathering
    /// OverdueTaskEmailContent does, and for the same reason. A line carries its entry's deadline where
    /// it has one, since a daily errand need not.
    /// </summary>
    public static (string Subject, string Body) Build(IReadOnlyList<DueDailyTaskReminder> reminders)
    {
        if (reminders is [var theOnlyOne])
        {
            return Build(theOnlyOne);
        }

        var subject = $"{reminders.Count} reminders";
        var body = string.Join(Environment.NewLine, [
            "These tasks are still waiting to be done:",
            .. reminders.Select(OneLineAbout)]);

        return (subject, body);
    }

    private static string OneLineAbout(DueDailyTaskReminder reminder)
    {
        var line = $"- \"{reminder.Description}\" from list \"{reminder.TaskListTitle}\"";
        return reminder.DueDateUtc is { } dueDateUtc
            ? $"{line} (due: {dueDateUtc.LocalDateTime:dd.MM.yyyy HH:mm})"
            : line;
    }
}
