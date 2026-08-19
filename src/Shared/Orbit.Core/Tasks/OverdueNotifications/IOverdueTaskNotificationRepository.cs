namespace Orbit.Core.Tasks.OverdueNotifications;

/// <summary>
/// Backs <see cref="OverdueTaskNotificationScheduler"/>: finds every incomplete, due-dated task item
/// (across all users) that could be overdue, and coordinates which ones have already been notified about
/// so the same one is never pushed twice - including when more than one
/// <c>OverdueTaskNotificationBackgroundService</c> instance polls at once.
/// </summary>
public interface IOverdueTaskNotificationRepository
{
    /// <summary>
    /// Every incomplete task item that has a due date set. Deliberately excludes an item that links to
    /// another task list (see <see cref="TaskItem.LinkedTaskListId"/>): its stored completion is always
    /// false regardless of whether the list it links to is actually done (see
    /// <see cref="LinkedTaskCompletionResolver"/>), so this repository - which queries the raw stored
    /// state, not the resolved one - can't tell a genuinely incomplete linked item from a completed one.
    /// </summary>
    Task<IReadOnlyList<OverdueTaskItem>> GetIncompleteWithDueDateAsync(CancellationToken cancellationToken);

    Task<bool> HasBeenNotifiedAsync(Guid taskItemId, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically reserves a single task item's overdue notification for the caller to send, using a
    /// unique constraint on <paramref name="taskItemId"/> as the concurrency guard. Returns false without
    /// throwing when another worker already reserved (or sent) it first.
    /// </summary>
    Task<bool> TryClaimAsync(Guid taskItemId, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Releases a reservation made by <see cref="TryClaimAsync"/> that failed to actually send, so it's
    /// picked up and retried on a later poll instead of being silently lost.
    /// </summary>
    Task ReleaseClaimAsync(Guid taskItemId, CancellationToken cancellationToken);
}
