using Microsoft.EntityFrameworkCore;
using Orbit.Contracts.Inventory;
using Orbit.Core.Sync;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Data;

/// <summary>
/// Every read and write a screen performs on warehouses - the same shape as the other three, including
/// the rule that each write records its own outbox entry in the same transaction as the change, and that
/// the offline policy is refused here rather than only shown on screen.
/// </summary>
public sealed class LocalWarehouseRepository
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly INetworkStatus _networkStatus;

    public LocalWarehouseRepository(
        IDbContextFactory<OrbitLocalDbContext> dbContextFactory, TimeProvider timeProvider, INetworkStatus networkStatus)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
        _networkStatus = networkStatus;
    }

    public async Task<IReadOnlyList<LocalWarehouse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Warehouses
            .AsNoTracking()
            .OrderByDescending(warehouse => warehouse.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<LocalWarehouse?> FindAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Warehouses.AsNoTracking()
            .FirstOrDefaultAsync(warehouse => warehouse.LocalId == localId, cancellationToken);
    }

    /// <summary>Whether this warehouse may be changed right now, without changing it.</summary>
    public async Task<bool> CanEditAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var warehouse = await dbContext.Warehouses.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.LocalId == localId, cancellationToken);

        return warehouse is not null && OfflineEditPolicy.IsAllowed(warehouse, _networkStatus);
    }

    public async Task<IReadOnlySet<Guid>> GetPendingLocalIdsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var localIds = await dbContext.Outbox
            .Where(entry => entry.EntityType == SyncEntityType.Warehouse)
            .Select(entry => entry.LocalId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return localIds.ToHashSet();
    }

    public async Task<LocalWarehouse> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var warehouse = new LocalWarehouse
        {
            LocalId = Guid.NewGuid(),
            ServerId = null,
            Name = name,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Warehouses.Add(warehouse);
        Enqueue(dbContext, warehouse.LocalId, OutboxOperation.Create, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return warehouse;
    }

    /// <summary>
    /// Saves the warehouse and <b>its whole intended item list</b> - items missing from it are deleted,
    /// which is what the API's save means. Refuses rather than queues when the offline policy forbids it.
    /// </summary>
    public async Task<LocalWriteOutcome> UpdateAsync(
        Guid localId, string name, IReadOnlyList<WarehouseItemDto> items, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Warehouses.FirstOrDefaultAsync(
                candidate => candidate.LocalId == localId, cancellationToken) is not { } warehouse)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (!OfflineEditPolicy.IsAllowed(warehouse, _networkStatus))
        {
            return LocalWriteOutcome.RefusedWhileOffline;
        }

        var now = _timeProvider.GetUtcNow();
        warehouse.Name = name;
        warehouse.Items = items;
        warehouse.UpdatedAtUtc = now;

        Enqueue(dbContext, localId, OutboxOperation.Update, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    public async Task<LocalWriteOutcome> DeleteAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Warehouses.FirstOrDefaultAsync(
                candidate => candidate.LocalId == localId, cancellationToken) is not { } warehouse)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (!OfflineEditPolicy.IsAllowed(warehouse, _networkStatus))
        {
            return LocalWriteOutcome.RefusedWhileOffline;
        }

        dbContext.Warehouses.Remove(warehouse);

        if (warehouse.ServerId is null)
        {
            dbContext.Outbox.RemoveRange(dbContext.Outbox.Where(
                entry => entry.EntityType == SyncEntityType.Warehouse && entry.LocalId == localId));
        }
        else
        {
            Enqueue(dbContext, localId, OutboxOperation.Delete, _timeProvider.GetUtcNow(), warehouse.ServerId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    private static void Enqueue(
        OrbitLocalDbContext dbContext, Guid localId, OutboxOperation operation, DateTimeOffset queuedAtUtc,
        Guid? serverId = null)
        => dbContext.Outbox.Add(new OutboxEntry
        {
            EntityType = SyncEntityType.Warehouse,
            LocalId = localId,
            ServerId = serverId,
            Operation = operation,
            QueuedAtUtc = queuedAtUtc
        });
}
