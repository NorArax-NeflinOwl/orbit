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

    /// <summary>
    /// One e-mail for everything that fell due in the same poll, a line per entry - the same gathering
    /// the push notification does (see <see cref="OverdueTaskPushContent"/>), because an inbox with four
    /// mails a minute apart is no easier to read than four banners nobody saw. One entry keeps the mail
    /// it always had.
    /// </summary>
    public static (string Subject, string Body) Build(IReadOnlyList<OverdueTaskItem> overdueTaskItems)
    {
        if (overdueTaskItems is [var theOnlyOne])
        {
            return Build(theOnlyOne);
        }

        var subject = $"{overdueTaskItems.Count} overdue tasks";
        var body = string.Join(Environment.NewLine, [
            "These tasks are overdue:",
            .. overdueTaskItems.Select(item =>
                $"- \"{item.Description}\" from list \"{item.TaskListTitle}\" " +
                $"(due: {item.DueDateUtc.LocalDateTime:dd.MM.yyyy HH:mm})")]);

        return (subject, body);
    }
}
