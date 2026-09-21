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
        return new PushNotificationPayload(
            "Overdue task", "Task \"{0}\" from list \"{1}\" is overdue.",
            [overdueTaskItem.Description, overdueTaskItem.TaskListTitle], $"/tasks/{overdueTaskItem.TaskListId}");
    }

    /// <summary>
    /// One notice for everything of an owner's that fell due in the same poll - see
    /// <see cref="SeveralEntriesAtOnce"/> for why several notices at once are worth less than one. A
    /// single entry says what it always said: the collective sentence is always about more than one, so
    /// nothing has to read "these tasks" about one task.
    /// </summary>
    public static PushNotificationPayload Build(IReadOnlyList<OverdueTaskItem> overdueTaskItems)
        => overdueTaskItems is [var theOnlyOne]
            ? Build(theOnlyOne)
            : new PushNotificationPayload(
                "Overdue tasks", "These tasks are overdue: {0}.",
                [SeveralEntriesAtOnce.Naming(
                    overdueTaskItems.Select(item => (item.Description, item.TaskListTitle)))],
                SeveralEntriesAtOnce.LeadingTo(overdueTaskItems.Select(item => item.TaskListId)));
}
