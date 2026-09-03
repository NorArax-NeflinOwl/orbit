namespace Orbit.Data.Entities;

/// <summary>
/// Reserves one task item's daily reminder for a given local calendar date, and doubles as the
/// permanent record that it was sent - the daily-reminder counterpart of
/// <see cref="TaskOverdueNotificationDeliveryEntity"/>. Keyed by (TaskItemId, ReminderDate) rather than
/// TaskItemId alone, since a "remind daily" item is eligible again every day it stays incomplete. The
/// row is inserted by DailyTaskReminderRepository.TryClaimAsync before the reminder actually goes out,
/// so the unique index on that pair is what stops two concurrent DailyTaskReminderBackgroundService
/// instances from ever sending the same day's reminder twice - not just a check performed beforehand.
/// </summary>
public sealed class TaskDailyReminderDeliveryEntity
{
    public Guid Id { get; set; }
    public Guid TaskItemId { get; set; }

    /// <summary>The local calendar date this delivery is for, stored as that date's local midnight.</summary>
    public DateTimeOffset ReminderDate { get; set; }

    public DateTimeOffset SentAtUtc { get; set; }
}
