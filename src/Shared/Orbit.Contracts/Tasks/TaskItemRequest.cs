namespace Orbit.Contracts.Tasks;

/// <summary>
/// OverdueNotificationChannel and DailyReminderNotificationChannel are each one of "None"/"Email"/
/// "Push"/"Both" (matches Orbit.Core.Notifications.NotificationChannel). DailyReminderTimeOfDay is the
/// local time of day the daily reminder is sent at when RemindDaily is set.
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
    TimeOnly DailyReminderTimeOfDay);
