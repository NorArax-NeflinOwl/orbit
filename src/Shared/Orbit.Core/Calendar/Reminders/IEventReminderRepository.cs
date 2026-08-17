namespace Orbit.Core.Calendar.Reminders;

/// <summary>
/// Backs <see cref="EventReminderScheduler"/>: finds every calendar event (across all users) that has
/// at least one reminder configured, and tracks which individual reminders have already been sent so
/// the same one is never emailed twice.
/// </summary>
public interface IEventReminderRepository
{
    Task<IReadOnlyList<CalendarEvent>> GetAllWithRemindersConfiguredAsync(CancellationToken cancellationToken);

    Task<bool> HasBeenSentAsync(Guid calendarEventId, int minutesBeforeStart, CancellationToken cancellationToken);

    Task MarkAsSentAsync(Guid calendarEventId, int minutesBeforeStart, DateTimeOffset sentAtUtc, CancellationToken cancellationToken);
}
