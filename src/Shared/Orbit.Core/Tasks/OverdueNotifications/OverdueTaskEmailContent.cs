namespace Orbit.Core.Tasks.OverdueNotifications;

/// <summary>Builds the subject and body of an e-mail sent when a task item has just become overdue.</summary>
public static class OverdueTaskEmailContent
{
    public static (string Subject, string Body) Build(OverdueTaskItem overdueTaskItem)
    {
        var subject = $"Zaległe zadanie: {overdueTaskItem.Description}";
        var body =
            $"Zadanie \"{overdueTaskItem.Description}\" z listy \"{overdueTaskItem.TaskListTitle}\" jest zaległe." +
            $"{Environment.NewLine}Termin: {overdueTaskItem.DueDateUtc.LocalDateTime:dd.MM.yyyy HH:mm}";

        return (subject, body);
    }
}
