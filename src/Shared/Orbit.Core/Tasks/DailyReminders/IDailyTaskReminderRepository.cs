namespace Orbit.Core.Tasks.DailyReminders;

/// <summary>
/// Backs <see cref="DailyTaskReminderScheduler"/>: finds every incomplete task item (across all users)
/// with "remind daily" enabled, and coordinates which (task item, local date) pairs have already been
/// notified about so the same day's reminder is never sent twice - including when more than one
/// <c>DailyTaskReminderBackgroundService</c> instance polls at once.
/// </summary>
public interface IDailyTaskReminderRepository
{
    /// <summary>
    /// Every task item with RemindDaily enabled and a non-"None" daily reminder channel that is still
    /// owed - **not one ticked off or crossed out**. "Remind daily" is asking about one errand until it
    /// is done, not a claim that the errand happens every day, and an entry finished with is not owed:
    /// asking again the next morning is the app arguing with the reader, and the reader's own list said
    /// so (2026-09-16, recorded in info/future-plan.md).
    ///
    /// The one exception is a shelf's standing round, which is work that comes back tomorrow whatever
    /// was done about it today - the thing this whole mechanism was built for. It is recognised here and
    /// nowhere else, comes back finished or not, and is the only candidate that carries
    /// <see cref="DailyTaskReminderCandidate.ComesRoundAgain"/>, which is what has it reopened (see
    /// <see cref="ReopenAsync"/>).
    ///
    /// Deliberately excludes an item that links to another task list (see
    /// <see cref="TaskItem.LinkedTaskListId"/>), for the same reason
    /// <see cref="Orbit.Core.Tasks.OverdueNotifications.IOverdueTaskNotificationRepository.GetIncompleteWithDueDateAsync"/>
    /// does.
    /// </summary>
    Task<IReadOnlyList<DailyTaskReminderCandidate>> GetEligibleAsync(CancellationToken cancellationToken);

    Task<bool> HasBeenSentAsync(Guid taskItemId, DateOnly reminderDate, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the item as not done again, so the day's reminder is about something still to do. A no-op
    /// for an item that was already open.
    ///
    /// Called only for an entry that comes round again - see
    /// <see cref="DailyTaskReminderCandidate.ComesRoundAgain"/>. An ordinary errand is never put through
    /// this: its tick and its deadline are the reader's, and a reminder is not a reason to take either.
    ///
    /// Everything <see cref="Orbit.Core.Tasks.TaskItem.Reopen"/> clears is cleared here - the tick, the
    /// cross, the time it was done and every way it was done by. The implementation holds the row rather
    /// than the aggregate, so the two are kept level by hand and by a test; they were not, and an entry
    /// done one of several ways came back with all its ways still taken, which the next read turned
    /// straight back into a tick.
    ///
    /// It also moves the entry's due date on to <paramref name="reminderDate"/>, at the hour the entry
    /// is reminded at - but only for an entry that already had one. That is what keeps a daily entry on
    /// the calendar and the dashboard, both of which read entries by their due date: without it the
    /// inventory's standing "Update stock levels" sat at the date it was created and went permanently
    /// overdue. An entry with no due date is left without one, since giving it a deadline nobody set
    /// would be inventing a promise.
    /// </summary>
    Task ReopenAsync(Guid taskItemId, DateOnly reminderDate, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically reserves a single (task item, local date) reminder for the caller to send, using a
    /// unique constraint on that pair as the concurrency guard. Returns false without throwing when
    /// another worker already reserved (or sent) it first.
    /// </summary>
    Task<bool> TryClaimAsync(Guid taskItemId, DateOnly reminderDate, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Releases a reservation made by <see cref="TryClaimAsync"/> that failed to actually send, so it's
    /// picked up and retried on a later poll instead of being silently lost.
    /// </summary>
    Task ReleaseClaimAsync(Guid taskItemId, DateOnly reminderDate, CancellationToken cancellationToken);
}
