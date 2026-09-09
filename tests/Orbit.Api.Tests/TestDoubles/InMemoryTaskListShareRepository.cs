using Orbit.Core.Tasks;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="ITaskListShareRepository"/> stub for unit tests that need real add/lookup/update
/// behavior, including per-recipient scoping, without spinning up SQLite. Mirrors InMemoryCalendarEventShareRepository.
/// </summary>
internal sealed class InMemoryTaskListShareRepository : ITaskListShareRepository
{
    private readonly List<TaskListShare> _shares = [];

    public Task AddAsync(TaskListShare share, CancellationToken cancellationToken)
    {
        _shares.Add(share);
        return Task.CompletedTask;
    }

    public Task<TaskListShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_shares.FirstOrDefault(share => share.Id == id && share.RecipientUserId == recipientUserId));

    public Task<TaskListShare?> FindExistingAsync(Guid sourceTaskListId, Guid recipientUserId, CancellationToken cancellationToken)
        => Task.FromResult(_shares.FirstOrDefault(share => share.SourceTaskListId == sourceTaskListId && share.RecipientUserId == recipientUserId));

    public Task<TaskListShare?> FindAcceptedGrantAsync(Guid sourceTaskListId, Guid recipientUserId, CancellationToken cancellationToken)
        => Task.FromResult(_shares.FirstOrDefault(
            share => share.SourceTaskListId == sourceTaskListId && share.RecipientUserId == recipientUserId && share.IsAccepted));

    public Task<IReadOnlyList<TaskListShare>> GetAcceptedGrantsForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken)
    {
        IReadOnlyList<TaskListShare> grants = _shares.Where(share => share.RecipientUserId == recipientUserId && share.IsAccepted).ToList();
        return Task.FromResult(grants);
    }

    public Task UpdateAsync(TaskListShare share, CancellationToken cancellationToken)
    {
        // Handlers mutate the same TaskListShare instance this repository already holds a reference to,
        // so there is nothing to replace here - mirrors InMemoryCalendarEventShareRepository.
        return Task.CompletedTask;
    }

    public Task<IReadOnlySet<Guid>> GetSharedOutTaskListIdsAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        IReadOnlySet<Guid> ids = _shares
            .Where(share => share.OwnerUserId == ownerUserId && share.IsAccepted)
            .Select(share => share.SourceTaskListId)
            .ToHashSet();

        return Task.FromResult(ids);
    }

    public Task RemoveAcceptedGrantAsync(Guid sourceId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        _shares.RemoveAll(share =>
            share.SourceTaskListId == sourceId && share.RecipientUserId == recipientUserId && share.IsAccepted);
        return Task.CompletedTask;
    }

    /// <summary>Everything this owner handed that recipient, oldest first - the real one orders the same way.</summary>
    public Task<IReadOnlyList<TaskListShare>> GetSharesToAsync(Guid ownerUserId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        IReadOnlyList<TaskListShare> shares = _shares
            .Where(share => share.OwnerUserId == ownerUserId && share.RecipientUserId == recipientUserId)
            .OrderBy(share => share.CreatedAtUtc)
            .ToList();

        return Task.FromResult(shares);
    }

    /// <summary>Scoped to the owner, exactly as the real one is: a share that is not theirs does not go.</summary>
    public Task<bool> RemoveAsync(Guid ownerUserId, Guid shareId, CancellationToken cancellationToken)
        => Task.FromResult(_shares.RemoveAll(share => share.Id == shareId && share.OwnerUserId == ownerUserId) > 0);
}
