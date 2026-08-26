using Orbit.Core.Tasks.DailyReminders;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IDailyTaskReminderRepository"/> stub for unit tests, standing in for the
/// cross-user daily-reminder query and claim/send tracking DailyTaskReminderRepository backs with
/// SQLite (mirrors <see cref="InMemoryOverdueTaskNotificationRepository"/> for overdue notifications).
/// </summary>
internal sealed class InMemoryDailyTaskReminderRepository : IDailyTaskReminderRepository
{
    private readonly List<DailyTaskReminderCandidate> _candidates;
    private readonly HashSet<(Guid TaskItemId, DateOnly ReminderDate)> _claimed = [];

    public InMemoryDailyTaskReminderRepository(IEnumerable<DailyTaskReminderCandidate> candidates)
    {
        _candidates = candidates.ToList();
    }

    public Task<IReadOnlyList<DailyTaskReminderCandidate>> GetEligibleAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<DailyTaskReminderCandidate>>(_candidates.ToList());

    public Task<bool> HasBeenSentAsync(Guid taskItemId, DateOnly reminderDate, CancellationToken cancellationToken)
        => Task.FromResult(_claimed.Contains((taskItemId, reminderDate)));

    public Task<bool> TryClaimAsync(Guid taskItemId, DateOnly reminderDate, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken)
        // HashSet<T>.Add already returns false when the pair is present, which is exactly the "someone
        // else claimed this first" signal TryClaimAsync needs - no separate lookup required.
        => Task.FromResult(_claimed.Add((taskItemId, reminderDate)));

    /// <summary>Which items the reminder loop brought back, so a test can check it happened.</summary>
    public List<Guid> Reopened { get; } = [];

    public Task ReopenAsync(Guid taskItemId, CancellationToken cancellationToken)
    {
        Reopened.Add(taskItemId);
        return Task.CompletedTask;
    }

    public Task ReleaseClaimAsync(Guid taskItemId, DateOnly reminderDate, CancellationToken cancellationToken)
    {
        _claimed.Remove((taskItemId, reminderDate));
        return Task.CompletedTask;
    }
}
