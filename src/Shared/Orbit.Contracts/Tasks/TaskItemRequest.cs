namespace Orbit.Contracts.Tasks;

/// <summary>
/// OverdueNotificationChannel and DailyReminderNotificationChannel are each one of "None"/"Email"/
/// "Push"/"Both" (matches Orbit.Core.Notifications.NotificationChannel). DailyReminderTimeOfDay is the
/// local time of day the daily reminder is sent at when RemindDaily is set.
///
/// Kind is "Checklist", "Calendar" or "Inventory" - see Orbit.Core.Tasks.TaskItemKind. Location says where a calendar
/// entry happens; it is ignored for every other kind, and for one carrying a LinkedCalendarEventId,
/// since that event already holds the place.
/// </summary>
public sealed record TaskItemRequest(
    string Description,
    /// <summary>
    /// The entry's existing id, or null for one the reader just added. Sent back so an entry keeps its
    /// identity across a save: other things point at a task entry by id - an inventory item's open
    /// restock task, a daily reminder's "already sent today" record, an overdue notification - and a
    /// save that minted fresh ids quietly cut every one of those loose. Mirrors WarehouseItemDto.Id.
    /// </summary>
    Guid? Id,
    DateTimeOffset? DueDateUtc,
    bool IsCompleted,
    Guid? LinkedTaskListId,
    string OverdueNotificationChannel,
    bool RemindDaily,
    string DailyReminderNotificationChannel,
    TimeOnly DailyReminderTimeOfDay,
    string Kind = "Checklist",
    string Location = "",
    Guid? LinkedCalendarEventId = null,
    /// <summary>
    /// The shelf item an Inventory entry is an errand about - see Orbit.Core.Tasks.TaskItem.LinkedInventoryItemId.
    /// Null for every other kind.
    /// </summary>
    Guid? LinkedInventoryItemId = null)
{
    /// <summary>
    /// An entry as it already is, ready to be sent back unchanged.
    ///
    /// Here rather than written out at each call site, because the endpoint replaces a list wholesale:
    /// every field has to ride along, and a caller that lists them by hand quietly drops whichever ones
    /// were added after it was written. Four of them had been - Kind, Location and the two links - so
    /// ticking a box on a checklist turned that list's inventory errands and appointments into plain
    /// lines and cut them loose from the shelf item and the event they were about. One mapping means a
    /// field added later is carried by everyone who saves a list.
    /// </summary>
    public static TaskItemRequest From(TaskItemDto item)
        => new(
            item.Description,
            item.Id,
            item.DueDateUtc,
            item.IsCompleted,
            item.LinkedTaskListId,
            item.OverdueNotificationChannel,
            item.RemindDaily,
            item.DailyReminderNotificationChannel,
            item.DailyReminderTimeOfDay,
            item.Kind,
            item.Location,
            item.LinkedCalendarEventId,
            item.LinkedInventoryItemId);
}
