using Microsoft.EntityFrameworkCore;
using Orbit.Contracts.Tasks;
using Orbit.Core.Sync;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Data;

/// <summary>
/// Every read and write a screen performs on task lists. The same shape as
/// <see cref="LocalNoteRepository"/>, deliberately: each write records its own outbox entry in the same
/// transaction as the change, because a local edit that was applied but not queued is silently lost at
/// the next pull.
/// </summary>
public sealed class LocalTaskListRepository
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly INetworkStatus _networkStatus;

    public LocalTaskListRepository(
        IDbContextFactory<OrbitLocalDbContext> dbContextFactory, TimeProvider timeProvider, INetworkStatus networkStatus)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
        _networkStatus = networkStatus;
    }

    public async Task<IReadOnlyList<LocalTaskList>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.TaskLists
            .AsNoTracking()
            // Pinned lists first, then most recently changed - the order the web client shows them in.
            .OrderByDescending(taskList => taskList.IsPinned)
            .ThenByDescending(taskList => taskList.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<LocalTaskList?> FindAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.TaskLists.AsNoTracking()
            .FirstOrDefaultAsync(taskList => taskList.LocalId == localId, cancellationToken);
    }

    /// <summary>
    /// Whether this list may be changed right now - the same question <see cref="UpdateAsync"/> asks
    /// before writing, so a screen and the write it leads to can never disagree. Asking by attempting a
    /// write would queue one, which is the opposite of what a read-only check is for.
    /// </summary>
    public async Task<bool> CanEditAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var taskList = await dbContext.TaskLists.AsNoTracking()
            .FirstOrDefaultAsync(list => list.LocalId == localId, cancellationToken);

        return taskList is not null && OfflineEditPolicy.IsAllowed(taskList, _networkStatus);
    }

    /// <summary>Which lists still have changes waiting to go out, so the screen can mark them.</summary>
    public async Task<IReadOnlySet<Guid>> GetPendingLocalIdsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var localIds = await dbContext.Outbox
            .Where(entry => entry.EntityType == SyncEntityType.TaskList)
            .Select(entry => entry.LocalId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return localIds.ToHashSet();
    }

    public async Task<LocalTaskList> CreateAsync(
        string title, IReadOnlyList<TaskItemDto> items, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var taskList = new LocalTaskList
        {
            LocalId = Guid.NewGuid(),
            ServerId = null,
            Title = title,
            Items = items,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.TaskLists.Add(taskList);
        Enqueue(dbContext, taskList.LocalId, OutboxOperation.Create, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return taskList;
    }

    /// <summary>Refuses rather than queues when the offline policy forbids it - see LocalWriteOutcome.</summary>
    public async Task<LocalWriteOutcome> UpdateAsync(
        Guid localId, string title, IReadOnlyList<TaskItemDto> items, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.TaskLists.FirstOrDefaultAsync(list => list.LocalId == localId, cancellationToken) is not { } taskList)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (!OfflineEditPolicy.IsAllowed(taskList, _networkStatus))
        {
            return LocalWriteOutcome.RefusedWhileOffline;
        }

        var now = _timeProvider.GetUtcNow();
        taskList.Title = title;
        taskList.Items = items;
        taskList.UpdatedAtUtc = now;
        // A list is done when every item is - the same rule the server applies.
        taskList.IsCompleted = items.Count > 0 && items.All(item => item.IsCompleted);

        Enqueue(dbContext, localId, OutboxOperation.Update, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <summary>
    /// Pins or unpins a list the user owns. Allowed offline whatever it is shared with, unlike an edit:
    /// the offline policy exists because two people with CanEdit are editing one row, and only the owner
    /// can pin - see SetTaskListPinnedCommandHandler - so there is no second writer. Mirrors
    /// LocalNoteRepository.SetPinnedAsync.
    /// </summary>
    public async Task<LocalWriteOutcome> SetPinnedAsync(
        Guid localId, bool isPinned, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.TaskLists.FirstOrDefaultAsync(candidate => candidate.LocalId == localId, cancellationToken) is not { } taskList)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (taskList.IsShared)
        {
            return LocalWriteOutcome.NotYours;
        }

        if (taskList.IsPinned == isPinned)
        {
            return LocalWriteOutcome.Applied;
        }

        taskList.IsPinned = isPinned;
        Enqueue(dbContext, localId, OutboxOperation.SetPinned, _timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    public async Task<LocalWriteOutcome> DeleteAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.TaskLists.FirstOrDefaultAsync(list => list.LocalId == localId, cancellationToken) is not { } taskList)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (!OfflineEditPolicy.IsAllowed(taskList, _networkStatus))
        {
            return LocalWriteOutcome.RefusedWhileOffline;
        }

        dbContext.TaskLists.Remove(taskList);

        // A list the server never saw has nothing to delete there, and dropping what was queued for it
        // stops replay creating the list the user has just thrown away.
        if (taskList.ServerId is null)
        {
            dbContext.Outbox.RemoveRange(dbContext.Outbox.Where(
                entry => entry.EntityType == SyncEntityType.TaskList && entry.LocalId == localId));
        }
        else
        {
            Enqueue(dbContext, localId, OutboxOperation.Delete, _timeProvider.GetUtcNow(), taskList.ServerId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    private static void Enqueue(
        OrbitLocalDbContext dbContext, Guid localId, OutboxOperation operation, DateTimeOffset queuedAtUtc,
        Guid? serverId = null)
        => dbContext.Outbox.Add(new OutboxEntry
        {
            EntityType = SyncEntityType.TaskList,
            LocalId = localId,
            ServerId = serverId,
            Operation = operation,
            QueuedAtUtc = queuedAtUtc
        });
}
