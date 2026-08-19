using Orbit.Core.Notifications;

namespace Orbit.Core.Tasks.OverdueNotifications;

/// <summary>Builds the push notification payload for a task item that has just become overdue.</summary>
public static class OverdueTaskPushContent
{
    public static PushNotificationPayload Build(OverdueTaskItem overdueTaskItem)
    {
        var body = $"Zadanie \"{overdueTaskItem.Description}\" z listy \"{overdueTaskItem.TaskListTitle}\" jest zaległe.";
        return new PushNotificationPayload("Zaległe zadanie", body, $"/tasks/{overdueTaskItem.TaskListId}");
    }
}
