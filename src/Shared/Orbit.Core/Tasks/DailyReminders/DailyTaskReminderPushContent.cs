using Orbit.Core.Notifications;

namespace Orbit.Core.Tasks.DailyReminders;

/// <summary>Builds the push notification payload for a task item's daily "remind daily" reminder.</summary>
public static class DailyTaskReminderPushContent
{
    public static PushNotificationPayload Build(DueDailyTaskReminder reminder)
    {
        var body = $"Zadanie \"{reminder.Description}\" z listy \"{reminder.TaskListTitle}\" wciąż czeka na wykonanie.";
        return new PushNotificationPayload("Przypomnienie o zadaniu", body, $"/tasks/{reminder.TaskListId}");
    }
}
