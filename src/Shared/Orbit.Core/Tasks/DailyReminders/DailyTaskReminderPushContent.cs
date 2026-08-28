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
        var body = $"Task \"{reminder.Description}\" from list \"{reminder.TaskListTitle}\" is still waiting to be done.";
        return new PushNotificationPayload("Task reminder", body, $"/tasks/{reminder.TaskListId}");
    }
}
