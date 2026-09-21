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

    /// <summary>
    /// One reminder for everything of an owner's that came due in the same poll - see
    /// <see cref="SeveralEntriesAtOnce"/>, and OverdueTaskPushContent, which gathers its notices the
    /// same way. A single reminder says what it always said.
    /// </summary>
    public static PushNotificationPayload Build(IReadOnlyList<DueDailyTaskReminder> reminders)
        => reminders is [var theOnlyOne]
            ? Build(theOnlyOne)
            : new PushNotificationPayload(
                "Task reminders", "These tasks are still waiting to be done: {0}.",
                [SeveralEntriesAtOnce.Naming(
                    reminders.Select(reminder => (reminder.Description, reminder.TaskListTitle)))],
                SeveralEntriesAtOnce.LeadingTo(reminders.Select(reminder => reminder.TaskListId)));
}
