namespace Orbit.Data.Entities;

/// <summary>
/// Reserves one task item's overdue push notification for a worker to send, and doubles as the
/// permanent record that it was sent - the task-item counterpart of
/// <see cref="EventReminderDeliveryEntity"/>. The row is inserted by
/// OverdueTaskNotificationRepository.TryClaimAsync before the notification actually goes out, so the
/// unique index on <see cref="TaskItemId"/> is what stops two concurrent
/// OverdueTaskNotificationBackgroundService instances from ever notifying about the same item twice -
/// not just a check performed beforehand.
/// </summary>
public sealed class TaskOverdueNotificationDeliveryEntity
{
    public Guid Id { get; set; }
    public Guid TaskItemId { get; set; }
    public DateTimeOffset SentAtUtc { get; set; }
}
