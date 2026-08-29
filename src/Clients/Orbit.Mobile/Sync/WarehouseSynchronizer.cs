using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orbit.Contracts.Inventory;
using Orbit.Core.Sync;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Sync;

/// <summary>
/// Brings warehouses and the server back into step. The last entity type of phase 4, and the only one
/// with a shape the spine did not already cover: the change feed says a warehouse changed but not what
/// is in it, because WarehouseDto carries no items. So a pull fetches the items of the warehouses whose
/// timestamp actually moved - not every warehouse, and not even every one the feed mentions, since an
/// inclusive cursor re-sends unchanged rows by design. That keeps the extra calls proportional to what
/// changed rather than to how much the user owns or how often they sync.
/// </summary>
public sealed class WarehouseSynchronizer
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly InventoryClient _inventoryClient;
    private readonly TimeProvider _timeProvider;
    private readonly SyncGate _syncGate;
    private readonly ILogger<WarehouseSynchronizer> _logger;

    public WarehouseSynchronizer(
        IDbContextFactory<OrbitLocalDbContext> dbContextFactory, InventoryClient inventoryClient,
        TimeProvider timeProvider, SyncGate syncGate, ILogger<WarehouseSynchronizer> logger)
    {
        _dbContextFactory = dbContextFactory;
        _inventoryClient = inventoryClient;
        _timeProvider = timeProvider;
        _syncGate = syncGate;
        _logger = logger;
    }

    /// <summary>Never throws for being offline - see NoteSynchronizer for why that is a rule here.</summary>
    public Task<SyncResult> SynchroniseAsync(CancellationToken cancellationToken = default)
        => _syncGate.RunAsync(SyncEntityType.Warehouse, () => RunAsync(cancellationToken), cancellationToken);

    private async Task<SyncResult> RunAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var push = await OutboxReplay.RunAsync(
            dbContext, SyncEntityType.Warehouse,
            (entry, token) => SendAsync(dbContext, entry, token), _logger, cancellationToken);

        try
        {
            var pull = await PullChangesAsync(dbContext, cancellationToken);
            return new SyncResult(push.Sent, pull.Received, pull.RemovedLocally, push.GivenUp, ReachedTheServer: true);
        }
        catch (Exception exception) when (SyncFailure.IsWorthRetrying(exception, cancellationToken))
        {
            _logger.LogInformation("Could not reach the server to pull warehouses ({Reason})", exception.Message);
            return push.Sent > 0
                ? new SyncResult(push.Sent, 0, 0, push.GivenUp, ReachedTheServer: true)
                : SyncResult.NeverGotThrough(push.GivenUp);
        }
    }

    private async Task<SendResult> SendAsync(OrbitLocalDbContext dbContext, OutboxEntry entry, CancellationToken cancellationToken)
    {
        if (entry.Operation is OutboxOperation.Delete)
        {
            if (entry.ServerId is not { } serverId)
            {
                return SendResult.Abandoned;
            }

            await _inventoryClient.DeleteAsync(serverId, cancellationToken);
            return SendResult.Sent;
        }

        var warehouse = await dbContext.Warehouses.FirstOrDefaultAsync(
            candidate => candidate.LocalId == entry.LocalId, cancellationToken);

        if (warehouse is null)
        {
            return SendResult.Abandoned;
        }

        return entry.Operation is OutboxOperation.Create
            ? await SendCreateAsync(warehouse, cancellationToken)
            : await SendUpdateAsync(warehouse, cancellationToken);
    }

    private async Task<SendResult> SendCreateAsync(LocalWarehouse warehouse, CancellationToken cancellationToken)
    {
        if (warehouse.ServerId is not null)
        {
            // Already created - a duplicate create would make a second warehouse out of one.
            return SendResult.Abandoned;
        }

        // Creating takes the name only; the items go up with the save that follows.
        warehouse.ServerId = await _inventoryClient.CreateAsync(
            new SaveWarehouseRequest(warehouse.Name, [], warehouse.IsPrivate), cancellationToken);
        warehouse.LastSyncedAtUtc = _timeProvider.GetUtcNow();
        return SendResult.Sent;
    }

    private async Task<SendResult> SendUpdateAsync(LocalWarehouse warehouse, CancellationToken cancellationToken)
    {
        if (warehouse.ServerId is not { } serverId)
        {
            // Its create is still queued ahead of this and has not succeeded yet.
            return SendResult.Abandoned;
        }

        var outcome = await _inventoryClient.UpdateAsync(
            serverId, new SaveWarehouseRequest(warehouse.Name, warehouse.Items, warehouse.IsPrivate), cancellationToken);

        if (outcome is not WriteOutcome.Applied)
        {
            _logger.LogInformation("The server refused an offline edit of warehouse {ServerId}: {Outcome}", serverId, outcome);
            return SendResult.Abandoned;
        }

        warehouse.LastSyncedAtUtc = _timeProvider.GetUtcNow();
        return SendResult.Sent;
    }

    private async Task<(int Received, int RemovedLocally)> PullChangesAsync(
        OrbitLocalDbContext dbContext, CancellationToken cancellationToken)
    {
        var cursor = await SyncCursors.ReadAsync(dbContext, SyncEntityType.Warehouse, cancellationToken);
        var feed = await _inventoryClient.GetChangesAsync(cursor, cancellationToken);

        var stillQueued = await dbContext.Outbox
            .Where(entry => entry.EntityType == SyncEntityType.Warehouse)
            .Select(entry => entry.LocalId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var received = 0;
        foreach (var incoming in feed.Changed)
        {
            var existing = await dbContext.Warehouses.FirstOrDefaultAsync(
                warehouse => warehouse.ServerId == incoming.Id, cancellationToken);

            if (existing is not null && stillQueued.Contains(existing.LocalId))
            {
                continue;
            }

            // The cursor is inclusive, so a pull re-sends things that have not actually changed. For
            // every other entity type that costs nothing - the same values are written again - but here
            // it would cost an HTTP call per warehouse, every sync, forever. Items only ever change
            // through the warehouse save, which moves UpdatedAtUtc, so that is a sound signal.
            var itemsMayHaveChanged = existing is null || existing.UpdatedAtUtc != incoming.UpdatedAtUtc;

            var warehouse = existing ?? NewLocalWarehouse(dbContext, incoming.Id);
            CopyInto(warehouse, incoming);

            if (itemsMayHaveChanged)
            {
                warehouse.Items = ToItems(await _inventoryClient.GetItemsAsync(incoming.Id, cancellationToken));
            }

            received++;
        }

        var removed = 0;
        foreach (var deletedId in feed.DeletedIds)
        {
            var warehouse = await dbContext.Warehouses.FirstOrDefaultAsync(
                candidate => candidate.ServerId == deletedId, cancellationToken);

            if (warehouse is null || stillQueued.Contains(warehouse.LocalId))
            {
                continue;
            }

            dbContext.Warehouses.Remove(warehouse);
            removed++;
        }

        await SyncCursors.WriteAsync(dbContext, SyncEntityType.Warehouse, feed.Cursor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (received, removed);
    }

    private static LocalWarehouse NewLocalWarehouse(OrbitLocalDbContext dbContext, Guid serverId)
    {
        var warehouse = new LocalWarehouse { LocalId = Guid.NewGuid(), ServerId = serverId };
        dbContext.Warehouses.Add(warehouse);
        return warehouse;
    }

    private void CopyInto(LocalWarehouse warehouse, WarehouseDto incoming)
    {
        warehouse.Name = incoming.Name;
        warehouse.IsPrivate = incoming.IsPrivate;
        warehouse.EncryptedCiphertext = incoming.EncryptedContent?.Ciphertext;
        warehouse.EncryptedNonce = incoming.EncryptedContent?.Nonce;
        warehouse.CreatedAtUtc = incoming.CreatedAtUtc;
        warehouse.UpdatedAtUtc = incoming.UpdatedAtUtc;
        warehouse.IsShared = incoming.IsShared;
        warehouse.SharedByUserName = incoming.SharedByUserName;
        warehouse.IsSharedWithOthers = incoming.IsSharedWithOthers;
        warehouse.AccessLevel = incoming.AccessLevel;
        warehouse.OwnerUserId = incoming.OriginalOwnerUserId;
        warehouse.LastSyncedAtUtc = _timeProvider.GetUtcNow();
    }

    /// <summary>
    /// What is read back becomes what would be saved. The read shape carries derived facts the save has
    /// no place for - whether an item is below its minimum, whether a restock task is already open - so
    /// only the fields a save actually sets are carried across. The id goes with it, because a save that
    /// minted fresh ids would cut loose whatever points at an item, exactly as it would for task entries.
    /// </summary>
    private static IReadOnlyList<WarehouseItemDto> ToItems(IReadOnlyList<InventoryItemDto> items)
        => items.Select(item => new WarehouseItemDto(
            item.Id, item.Name, item.ProductType, item.Category, item.Quantity, item.MinimumQuantity,
            item.Unit, item.ExpiryDate?.ToUniversalTime(), item.ExpiryNotificationChannel)).ToList();
}
