namespace Orbit.Core.Calendar.Reminders;

/// <summary>
/// Backs <see cref="EventReminderScheduler"/>: finds every calendar event (across all users) that has
/// at least one reminder configured and "approaching event" notifications turned on, and coordinates
/// which individual reminders have already been sent or reserved so the same one is never emailed twice
/// - including when more than one <c>CalendarEventReminderBackgroundService</c> instance polls at once.
/// </summary>
public interface IEventReminderRepository
{
    Task<IReadOnlyList<CalendarEvent>> GetAllWithRemindersConfiguredAsync(CancellationToken cancellationToken);

    Task<bool> HasBeenSentAsync(Guid calendarEventId, int minutesBeforeStart, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically reserves a single reminder for the caller to send, using a unique constraint on
    /// (<paramref name="calendarEventId"/>, <paramref name="minutesBeforeStart"/>) as the concurrency
    /// guard. Returns false without throwing when another worker already reserved (or sent) the same
    /// reminder first - the caller should treat that as "someone else is handling this" and move on,
    /// never as an error.
    /// </summary>
    Task<bool> TryClaimAsync(Guid calendarEventId, int minutesBeforeStart, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Releases a reservation made by <see cref="TryClaimAsync"/> that failed to actually send, so the
    /// reminder is picked up and retried on a later poll instead of being silently lost.
    /// </summary>
    Task ReleaseClaimAsync(Guid calendarEventId, int minutesBeforeStart, CancellationToken cancellationToken);
}
