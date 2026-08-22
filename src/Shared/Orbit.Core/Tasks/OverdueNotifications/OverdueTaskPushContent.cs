using Orbit.Core.Notifications;

namespace Orbit.Core.Tasks.OverdueNotifications;

/// <summary>Builds the push notification payload for a task item that has just become overdue.</summary>
public static class OverdueTaskPushContent
{
    public static PushNotificationPayload Build(OverdueTaskItem overdueTaskItem)
    {
        var body = $"Task \"{overdueTaskItem.Description}\" from list \"{overdueTaskItem.TaskListTitle}\" is overdue.";
        return new PushNotificationPayload("Overdue task", body, $"/tasks/{overdueTaskItem.TaskListId}");
    }
}
