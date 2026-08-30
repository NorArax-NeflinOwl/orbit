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
public sealed record TaskItemDto(
    Guid Id,
    string Description,
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
    Guid? LinkedInventoryItemId = null);
