using Orbit.Core.Notifications;

namespace Orbit.Core.Tasks.DailyReminders;

/// <summary>
/// Builds the push notification payload for a task item's daily "remind daily" reminder. The link is to
/// the list itself, which opens as its checklist - see OverdueTaskPushContent for why.
/// </summary>
public static class DailyTaskReminderPushContent
{
    public static PushNotificationPayload Build(DueDailyTaskReminder reminder)
    {
        return new PushNotificationPayload(
            "Task reminder", "Task \"{0}\" from list \"{1}\" is still waiting to be done.",
            [reminder.Description, reminder.TaskListTitle], $"/tasks/{reminder.TaskListId}");
    }
}
