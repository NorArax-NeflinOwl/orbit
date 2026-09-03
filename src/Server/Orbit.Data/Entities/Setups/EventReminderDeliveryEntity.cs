namespace Orbit.Data.Entities;

/// <summary>
/// Reserves one specific occurrence's reminder (a calendar event + a lead time from
/// <see cref="CalendarEventEntity.RemindersJson"/> + which occurrence of the event, for a recurring one)
/// for a worker to send, and doubles as the permanent record that it was sent. The row is inserted by
/// EventReminderRepository.TryClaimAsync before the email actually goes out, so the unique index on
/// (CalendarEventId, MinutesBeforeStart, OccurrenceStartUtc) is what stops two concurrent
/// CalendarEventReminderBackgroundService instances from ever sending the same reminder twice - not just a
/// check performed beforehand.
/// </summary>
public sealed class EventReminderDeliveryEntity
{
    public Guid Id { get; set; }
    public Guid CalendarEventId { get; set; }
    public int MinutesBeforeStart { get; set; }

    /// <summary>
    /// Which occurrence of CalendarEventId this claim is for - equal to the event's own StartUtc for a
    /// non-recurring event, or one specific generated occurrence for a recurring one (see
    /// CalendarEventOccurrenceGenerator). Part of the row's uniqueness so a recurring event's reminders
    /// are tracked per occurrence instead of only ever firing once for the whole series.
    /// </summary>
    public DateTimeOffset OccurrenceStartUtc { get; set; }

    public DateTimeOffset SentAtUtc { get; set; }
}
