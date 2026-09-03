using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orbit.Contracts.Inventories;
using Orbit.Core.Sync;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Sync;

/// <summary>
/// Brings inventories and the server back into step. The last entity type of phase 4, and the only one
/// with a shape the spine did not already cover: the change feed says an inventory changed but not what
/// is in it, because InventoryDto carries no items. So a pull fetches the items of the inventories whose
/// timestamp actually moved - not every inventory, and not even every one the feed mentions, since an
/// inclusive cursor re-sends unchanged rows by design. That keeps the extra calls proportional to what
/// changed rather than to how much the user owns or how often they sync.
/// </summary>
public sealed class InventorySynchronizer
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly InventoryClient _inventoryClient;
    private readonly TimeProvider _timeProvider;
    private readonly SyncGate _syncGate;
    private readonly ILogger<InventorySynchronizer> _logger;

    public InventorySynchronizer(
        IDbContextFactory<OrbitLocalDbContext> dbContextFactory, InventoryClient inventoryClient,
        TimeProvider timeProvider, SyncGate syncGate, ILogger<InventorySynchronizer> logger)
    {
        _dbContextFactory = dbContextFactory;
        _inventoryClient = inventoryClient;
        _timeProvider = timeProvider;
        _syncGate = syncGate;
        _logger = logger;
    }

    /// <summary>Never throws for being offline - see NoteSynchronizer for why that is a rule here.</summary>
    public Task<SyncResult> SynchroniseAsync(CancellationToken cancellationToken = default)
        => _syncGate.RunAsync(SyncEntityType.Inventory, () => RunAsync(cancellationToken), cancellationToken);

    private async Task<SyncResult> RunAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var push = await OutboxReplay.RunAsync(
            dbContext, SyncEntityType.Inventory,
            (entry, token) => SendAsync(dbContext, entry, token), _timeProvider, _logger, cancellationToken);

        try
        {
            var pull = await PullChangesAsync(dbContext, cancellationToken);
            return new SyncResult(push.Sent, pull.Received, pull.RemovedLocally, push.GivenUp, ReachedTheServer: true);
        }
        catch (Exception exception) when (SyncFailure.IsWorthRetrying(exception, cancellationToken))
        {
            _logger.LogInformation("Could not reach the server to pull inventories ({Reason})", exception.Message);
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

        var inventory = await dbContext.Inventories.FirstOrDefaultAsync(
            candidate => candidate.LocalId == entry.LocalId, cancellationToken);

        if (inventory is null)
        {
            return SendResult.Abandoned;
        }

        return entry.Operation is OutboxOperation.Create
            ? await SendCreateAsync(inventory, cancellationToken)
            : await SendUpdateAsync(inventory, cancellationToken);
    }

    private async Task<SendResult> SendCreateAsync(LocalInventory inventory, CancellationToken cancellationToken)
    {
        if (inventory.ServerId is not null)
        {
            // Already created - a duplicate create would make a second inventory out of one.
            return SendResult.Abandoned;
        }

        // Creating takes the name only; the items go up with the save that follows. A private
        // inventory's name is in EncryptedContent and its readable fields are empty, which is how the
        // row is already stored - see LocalInventoryRepository.
        inventory.ServerId = await _inventoryClient.CreateAsync(
            new SaveInventoryRequest(
                inventory.Name, [], inventory.IsPrivate, inventory.EncryptedContent, inventory.Description),
            cancellationToken);
        inventory.LastSyncedAtUtc = _timeProvider.GetUtcNow();
        return SendResult.Sent;
    }

    private async Task<SendResult> SendUpdateAsync(LocalInventory inventory, CancellationToken cancellationToken)
    {
        if (inventory.ServerId is not { } serverId)
        {
            // Its create is still queued ahead of this and has not succeeded yet.
            return SendResult.Abandoned;
        }

        var outcome = await _inventoryClient.UpdateAsync(
            serverId,
            // Said rather than left out, for the reason CreateTaskRequest gives: null keeps what is
            // stored, so a description cleared here would come back at the next pull.
            new SaveInventoryRequest(
                inventory.Name, inventory.Items, inventory.IsPrivate, inventory.EncryptedContent,
                inventory.Description),
            cancellationToken);

        if (outcome is not WriteOutcome.Applied)
        {
            _logger.LogInformation("The server refused an offline edit of inventory {ServerId}: {Outcome}", serverId, outcome);
            return SendResult.Refused;
        }

        inventory.LastSyncedAtUtc = _timeProvider.GetUtcNow();
        return SendResult.Sent;
    }

    private async Task<(int Received, int RemovedLocally)> PullChangesAsync(
        OrbitLocalDbContext dbContext, CancellationToken cancellationToken)
    {
        var cursor = await SyncCursors.ReadAsync(dbContext, SyncEntityType.Inventory, cancellationToken);
        var feed = await _inventoryClient.GetChangesAsync(cursor, cancellationToken);

        var stillQueued = await dbContext.Outbox
            .Where(entry => entry.EntityType == SyncEntityType.Inventory)
            .Select(entry => entry.LocalId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var received = 0;
        foreach (var incoming in feed.Changed)
        {
            var existing = await dbContext.Inventories.FirstOrDefaultAsync(
                inventory => inventory.ServerId == incoming.Id, cancellationToken);

            if (existing is not null && stillQueued.Contains(existing.LocalId))
            {
                continue;
            }

            // The cursor is inclusive, so a pull re-sends things that have not actually changed. For
            // every other entity type that costs nothing - the same values are written again - but here
            // it would cost an HTTP call per inventory, every sync, forever. Items only ever change
            // through the inventory save, which moves UpdatedAtUtc, so that is a sound signal.
            var itemsMayHaveChanged = existing is null || existing.UpdatedAtUtc != incoming.UpdatedAtUtc;

            var inventory = existing ?? NewLocalInventory(dbContext, incoming.Id);
            CopyInto(inventory, incoming);

            if (itemsMayHaveChanged)
            {
                var onTheShelf = await _inventoryClient.GetItemsAsync(incoming.Id, cancellationToken);
                inventory.Items = ToItems(onTheShelf);
                // When each batch arrived, which the save shape does not carry - see
                // LocalInventory.ItemArrivals.
                inventory.ItemArrivals = onTheShelf.ToDictionary(item => item.Id, item => item.CreatedAtUtc);
            }

            received++;
        }

        var removed = 0;
        foreach (var deletedId in feed.DeletedIds)
        {
            var inventory = await dbContext.Inventories.FirstOrDefaultAsync(
                candidate => candidate.ServerId == deletedId, cancellationToken);

            if (inventory is null || stillQueued.Contains(inventory.LocalId))
            {
                continue;
            }

            dbContext.Inventories.Remove(inventory);
            removed++;
        }

        await SyncCursors.WriteAsync(dbContext, SyncEntityType.Inventory, feed.Cursor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (received, removed);
    }

    private static LocalInventory NewLocalInventory(OrbitLocalDbContext dbContext, Guid serverId)
    {
        var inventory = new LocalInventory { LocalId = Guid.NewGuid(), ServerId = serverId };
        dbContext.Inventories.Add(inventory);
        return inventory;
    }

    private void CopyInto(LocalInventory inventory, InventoryDto incoming)
    {
        inventory.Name = incoming.Name;
        inventory.Description = incoming.Description;
        inventory.IsPrivate = incoming.IsPrivate;
        inventory.EncryptedCiphertext = incoming.EncryptedContent?.Ciphertext;
        inventory.EncryptedNonce = incoming.EncryptedContent?.Nonce;
        inventory.CreatedAtUtc = incoming.CreatedAtUtc;
        inventory.UpdatedAtUtc = incoming.UpdatedAtUtc;
        inventory.IsShared = incoming.IsShared;
        inventory.SharedByUserName = incoming.SharedByUserName;
        inventory.IsSharedWithOthers = incoming.IsSharedWithOthers;
        inventory.AccessLevel = incoming.AccessLevel;
        inventory.OwnerUserId = incoming.OriginalOwnerUserId;
        inventory.LastSyncedAtUtc = _timeProvider.GetUtcNow();
    }

    /// <summary>
    /// What is read back becomes what would be saved. The read shape carries derived facts the save has
    /// no place for - whether an item is below its minimum, whether a restock task is already open - so
    /// only the fields a save actually sets are carried across. The id goes with it, because a save that
    /// minted fresh ids would cut loose whatever points at an item, exactly as it would for task entries.
    /// </summary>
    private static IReadOnlyList<InventoryItemRequest> ToItems(IReadOnlyList<InventoryItemDto> items)
        => items.Select(item => new InventoryItemRequest(
            item.Id, item.Name, item.ProductType, item.Category, item.Quantity, item.MinimumQuantity,
            item.Unit, item.ExpiryDate?.ToUniversalTime(), item.ExpiryNotificationChannel,
            // Carried rather than left to mean "not provided": the read shape always says what it is,
            // and a save that says nothing cannot turn it off.
            item.IsCheckedRegularly)).ToList();
}
