namespace Orbit.Data.Entities;

/// <summary>
/// Reserves one specific reminder (a calendar event + a lead time from
/// <see cref="CalendarEventEntity.RemindersJson"/>) for a worker to send, and doubles as the permanent
/// record that it was sent. The row is inserted by EventReminderRepository.TryClaimAsync before the
/// email actually goes out, so the unique index on (CalendarEventId, MinutesBeforeStart) is what stops
/// two concurrent CalendarEventReminderBackgroundService instances from ever sending the same reminder
/// twice - not just a check performed beforehand.
/// </summary>
public sealed class EventReminderDeliveryEntity
{
    public Guid Id { get; set; }
    public Guid CalendarEventId { get; set; }
    public int MinutesBeforeStart { get; set; }
    public DateTimeOffset SentAtUtc { get; set; }
}
