using Orbit.Core.Notifications;

namespace Orbit.Core.Tasks.OverdueNotifications;

/// <summary>
/// Builds the push notification payload for a task item that has just become overdue. The link is to the
/// list itself, which opens as its checklist - somebody told a task is overdue is being told to go and
/// tick it off, not to go and rewrite the list (see TaskListChecklist in Orbit.Web).
/// </summary>
public static class OverdueTaskPushContent
{
    public static PushNotificationPayload Build(OverdueTaskItem overdueTaskItem)
    {
        var body = $"Task \"{overdueTaskItem.Description}\" from list \"{overdueTaskItem.TaskListTitle}\" is overdue.";
        return new PushNotificationPayload("Overdue task", body, $"/tasks/{overdueTaskItem.TaskListId}");
    }
}
