namespace Orbit.Contracts.Tasks;

/// <summary>
/// OverdueNotificationChannel and DailyReminderNotificationChannel are each one of "None"/"Email"/
/// "Push"/"Both" (matches Orbit.Core.Notifications.NotificationChannel). DailyReminderTimeOfDay is the
/// local time of day the daily reminder is sent at when RemindDaily is set.
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
    TimeOnly DailyReminderTimeOfDay);
