namespace Orbit.Data.Entities;

/// <summary>
/// Marks one specific reminder (a calendar event + a lead time from
/// <see cref="CalendarEventEntity.RemindersJson"/>) as already emailed, so
/// Orbit.Api's CalendarEventReminderBackgroundService never sends the same reminder twice.
/// </summary>
public sealed class EventReminderDeliveryEntity
{
    public Guid Id { get; set; }
    public Guid CalendarEventId { get; set; }
    public int MinutesBeforeStart { get; set; }
    public DateTimeOffset SentAtUtc { get; set; }
}
