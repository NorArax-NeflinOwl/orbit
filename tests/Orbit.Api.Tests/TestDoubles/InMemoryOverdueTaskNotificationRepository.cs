using Orbit.Core.Tasks.OverdueNotifications;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IOverdueTaskNotificationRepository"/> stub for unit tests, standing in for the
/// cross-user overdue-task query and claim/send tracking OverdueTaskNotificationRepository backs with
/// SQLite (mirrors <see cref="InMemoryEventReminderRepository"/> for calendar event reminders).
/// </summary>
internal sealed class InMemoryOverdueTaskNotificationRepository : IOverdueTaskNotificationRepository
{
    private readonly List<OverdueTaskItem> _candidates;
    private readonly HashSet<Guid> _claimedTaskItemIds = [];

    public InMemoryOverdueTaskNotificationRepository(IEnumerable<OverdueTaskItem> candidates)
    {
        _candidates = candidates.ToList();
    }

    public Task<IReadOnlyList<OverdueTaskItem>> GetIncompleteWithDueDateAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<OverdueTaskItem>>(_candidates.ToList());

    public Task<bool> HasBeenNotifiedAsync(Guid taskItemId, CancellationToken cancellationToken)
        => Task.FromResult(_claimedTaskItemIds.Contains(taskItemId));

    public Task<bool> TryClaimAsync(Guid taskItemId, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken)
        // HashSet<T>.Add already returns false when the item is present, which is exactly the "someone
        // else claimed this first" signal TryClaimAsync needs - no separate lookup required.
        => Task.FromResult(_claimedTaskItemIds.Add(taskItemId));

    public Task ReleaseClaimAsync(Guid taskItemId, CancellationToken cancellationToken)
    {
        _claimedTaskItemIds.Remove(taskItemId);
        return Task.CompletedTask;
    }
}
