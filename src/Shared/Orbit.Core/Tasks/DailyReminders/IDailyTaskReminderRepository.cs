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
    /// Every incomplete task item with RemindDaily enabled and a non-"None" daily reminder channel.
    /// Deliberately excludes an item that links to another task list (see
    /// <see cref="TaskItem.LinkedTaskListId"/>), for the same reason
    /// <see cref="Orbit.Core.Tasks.OverdueNotifications.IOverdueTaskNotificationRepository.GetIncompleteWithDueDateAsync"/>
    /// does.
    /// </summary>
    Task<IReadOnlyList<DailyTaskReminderCandidate>> GetEligibleAsync(CancellationToken cancellationToken);

    Task<bool> HasBeenSentAsync(Guid taskItemId, DateOnly reminderDate, CancellationToken cancellationToken);

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
