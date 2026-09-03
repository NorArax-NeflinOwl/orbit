using Orbit.Core.Tasks;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="ITaskRepository"/> stub for unit tests that need real add/lookup/update
/// behavior, including per-user ownership scoping, without spinning up SQLite.
/// </summary>
internal sealed class InMemoryTaskRepository : ITaskRepository
{
    private readonly List<TaskList> _taskLists = [];

    public Task<IReadOnlyList<TaskList>> GetAllAsync(
        Guid userId, DateTimeOffset? updatedSinceUtc, CancellationToken cancellationToken)
    {
        var matching = _taskLists.Where(taskList => taskList.UserId == userId);
        if (updatedSinceUtc is not null)
        {
            matching = matching.Where(taskList => taskList.UpdatedAtUtc >= updatedSinceUtc.Value);
        }

        return Task.FromResult<IReadOnlyList<TaskList>>(matching.ToList());
    }

    public Task<TaskList?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_taskLists.FirstOrDefault(taskList => taskList.Id == id && taskList.UserId == userId));

    public Task<IReadOnlyList<TaskList>> GetHoldingItemsAsync(
        Guid userId, Guid exceptListId, IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<TaskList>>(
            _taskLists.Where(taskList => taskList.UserId == userId
                    && taskList.Id != exceptListId
                    && taskList.Items.Any(item => itemIds.Contains(item.Id)))
                .ToList());

    public Task AddAsync(TaskList taskList, CancellationToken cancellationToken)
    {
        _taskLists.Add(taskList);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TaskList taskList, CancellationToken cancellationToken)
    {
        // Handlers mutate the same TaskList instance this repository already holds a reference to, so
        // there is nothing to replace here - this mirrors how the EF Core repository just calls
        // SaveChangesAsync on an already-tracked entity.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Counted rather than performed: the real one writes three columns and leaves the entries alone
    /// (see TaskRepository.UpdateLockAsync), and what a test can check here is that a lock took this
    /// path rather than the one that rewrites the whole list.
    /// </summary>
    public Task UpdateLockAsync(TaskList taskList, CancellationToken cancellationToken)
    {
        LockSaves++;
        return Task.CompletedTask;
    }

    /// <summary>How many times a lock was saved on its own - see UpdateLockAsync.</summary>
    public int LockSaves { get; private set; }

    public Task UpdateManyAsync(IReadOnlyList<TaskList> taskLists, CancellationToken cancellationToken)
    {
        // Same reasoning as UpdateAsync - every list here is already the same tracked instance.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Puts a list's "last changed" back to <paramref name="updatedAtUtc"/>, and hands back the copy
    /// that is stored from then on.
    ///
    /// For the tests that assert a timestamp was rewritten. The domain stamps DateTimeOffset.UtcNow
    /// itself - there is no clock to inject anywhere near TaskList - so reading the stamp, making one
    /// call and comparing is really asking whether the clock ticked in between. On a machine quick
    /// enough it does not, and the test fails for being fast rather than for being wrong: one of them
    /// failed exactly once, in a run of all three test assemblies at the same time. Ageing the list
    /// first puts the comparison back on what it is supposed to be about.
    /// </summary>
    public TaskList PretendItWasLastChanged(Guid id, DateTimeOffset updatedAtUtc)
    {
        var stored = _taskLists.Single(taskList => taskList.Id == id);
        var aged = TaskList.FromPersistence(
            stored.Id, stored.UserId, stored.Title, stored.Items, stored.IsGroup, stored.IsPrivate,
            stored.EncryptedContent, stored.CreatedAtUtc, updatedAtUtc,
            stored.LockedByUserId, stored.LockedByUserName, stored.LockExpiresAtUtc,
            stored.Priority, stored.IsPinned, stored.LinkedInventoryId);

        // Carried over rather than left at their defaults: FromPersistence does not take them, and a
        // shared list that came back unshared would be a different list from the one being tested.
        aged.SetAccessContext(stored.IsShared, stored.SharedByUserName, stored.AccessLevel);
        aged.SetSharedWithOthers(stored.IsSharedWithOthers);

        _taskLists[_taskLists.IndexOf(stored)] = aged;
        return aged;
    }

    public Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        _taskLists.RemoveAll(taskList => taskList.Id == id && taskList.UserId == userId);
        return Task.CompletedTask;
    }
}
