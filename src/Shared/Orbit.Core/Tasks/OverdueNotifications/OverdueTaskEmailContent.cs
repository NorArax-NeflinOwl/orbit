namespace Orbit.Core.Tasks.OverdueNotifications;

/// <summary>Builds the subject and body of an e-mail sent when a task item has just become overdue.</summary>
public static class OverdueTaskEmailContent
{
    public static (string Subject, string Body) Build(OverdueTaskItem overdueTaskItem)
    {
        var subject = $"Overdue task: {overdueTaskItem.Description}";
        var body =
            $"Task \"{overdueTaskItem.Description}\" from list \"{overdueTaskItem.TaskListTitle}\" is overdue." +
            $"{Environment.NewLine}Due: {overdueTaskItem.DueDateUtc.LocalDateTime:dd.MM.yyyy HH:mm}";

        return (subject, body);
    }
}
