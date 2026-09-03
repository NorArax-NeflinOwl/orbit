using Microsoft.EntityFrameworkCore;
using Orbit.Contracts.Inventories;
using Orbit.Core.Sync;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Data;

/// <summary>
/// An inventory as the reader has just left it. The same shape as <see cref="NoteContent"/> and
/// <see cref="TaskListContent"/>, for the same reason: the three repositories are written alike, so a
/// change made to one is obvious in the others.
/// </summary>
/// <param name="IsPrivate"><inheritdoc cref="NoteContent.IsPrivate" path="/summary"/></param>
/// <param name="Description"><inheritdoc cref="TaskListContent.Description" path="/summary"/></param>
public sealed record InventoryContent(
    string Name, IReadOnlyList<InventoryItemRequest> Items, bool IsPrivate = false, string Description = "");

/// <summary>
/// Every read and write a screen performs on inventories - the same shape as the other three, including
/// the rule that each write records its own outbox entry in the same transaction as the change, and that
/// the offline policy is refused here rather than only shown on screen.
/// </summary>
public sealed class LocalInventoryRepository : ICopyReviewStore
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly INetworkStatus _networkStatus;
    private readonly PrivateContentSealer _privateContent;

    public LocalInventoryRepository(
        IDbContextFactory<OrbitLocalDbContext> dbContextFactory, TimeProvider timeProvider, INetworkStatus networkStatus,
        PrivateContentSealer privateContent)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
        _networkStatus = networkStatus;
        _privateContent = privateContent;
    }

    public async Task<IReadOnlyList<LocalInventory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var inventories = await dbContext.Inventories
            .AsNoTracking()
            .OrderByDescending(inventory => inventory.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        await OpenPrivateContentAsync(inventories, cancellationToken);
        return inventories;
    }

    public async Task<LocalInventory?> FindAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var inventory = await dbContext.Inventories.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.LocalId == localId, cancellationToken);

        if (inventory is not null)
        {
            await OpenPrivateContentAsync([inventory], cancellationToken);
        }

        return inventory;
    }

    /// <inheritdoc cref="LocalNoteRepository.OpenPrivateContentAsync"/>
    private async Task OpenPrivateContentAsync(
        IReadOnlyList<LocalInventory> inventories, CancellationToken cancellationToken)
    {
        var privateInventories = inventories.Where(inventory => inventory.IsPrivate).ToList();
        if (privateInventories.Count == 0)
        {
            return;
        }

        PrivateContentKey key;
        try
        {
            key = await _privateContent.UnlockAsync(cancellationToken);
        }
        catch (EncryptionKeyLockedException)
        {
            foreach (var inventory in privateInventories)
            {
                inventory.IsSealed = true;
            }

            return;
        }

        using (key)
        {
            foreach (var inventory in privateInventories)
            {
                Open(key, inventory);
            }
        }
    }

    private static void Open(PrivateContentKey key, LocalInventory inventory)
    {
        if (inventory.EncryptedContent is not { } encryptedContent
            || key.Open(encryptedContent, SealedContentSerializerContext.Default.SealedInventory) is not { } opened)
        {
            inventory.IsSealed = true;
            return;
        }

        inventory.Name = opened.Name;
        inventory.Items = opened.Items;
        inventory.IsSealed = false;
    }

    /// <summary>Whether this inventory may be changed right now, without changing it.</summary>
    public async Task<bool> CanEditAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var inventory = await dbContext.Inventories.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.LocalId == localId, cancellationToken);

        return inventory is not null && SharedItemAccess.AllowsEditing(inventory) && OfflineEditPolicy.IsAllowed(inventory, _networkStatus);
    }

    public async Task<IReadOnlySet<Guid>> GetPendingLocalIdsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var localIds = await dbContext.Outbox
            .Where(entry => entry.EntityType == SyncEntityType.Inventory)
            .Select(entry => entry.LocalId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return localIds.ToHashSet();
    }

    public async Task<LocalInventory> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var inventory = new LocalInventory
        {
            LocalId = Guid.NewGuid(),
            ServerId = null,
            Name = name,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Inventories.Add(inventory);
        Enqueue(dbContext, inventory.LocalId, OutboxOperation.Create, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return inventory;
    }

    /// <summary>
    /// Saves the inventory and <b>its whole intended item list</b> - items missing from it are deleted,
    /// which is what the API's save means. Refuses rather than queues when the offline policy forbids it.
    /// </summary>
    public async Task<LocalWriteOutcome> UpdateAsync(
        Guid localId, InventoryContent content, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Inventories.FirstOrDefaultAsync(
                candidate => candidate.LocalId == localId, cancellationToken) is not { } inventory)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (!SharedItemAccess.AllowsEditing(inventory))
        {
            return LocalWriteOutcome.RefusedAsReadOnly;
        }

        if (!OfflineEditPolicy.IsAllowed(inventory, _networkStatus))
        {
            return LocalWriteOutcome.RefusedWhileOffline;
        }

        var now = _timeProvider.GetUtcNow();
        await WriteContentAsync(inventory, content, cancellationToken);
        inventory.UpdatedAtUtc = now;

        // A copy still awaiting review is written to this phone and queued for nobody: what it is has
        // not been decided yet, and the review is what sends it - see LocalNoteRepository.UpdateAsync.
        if (!CopiesForEditing.IsAwaitingReview(inventory))
        {
            Enqueue(dbContext, localId, OutboxOperation.Update, now);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <inheritdoc cref="LocalNoteRepository.WriteContentAsync"/>
    private async Task WriteContentAsync(
        LocalInventory inventory, InventoryContent content, CancellationToken cancellationToken)
    {
        inventory.IsPrivate = content.IsPrivate;
        inventory.IsSealed = false;

        if (!content.IsPrivate)
        {
            inventory.Name = content.Name;
            inventory.Description = content.Description;
            inventory.Items = content.Items;
            inventory.EncryptedCiphertext = null;
            inventory.EncryptedNonce = null;
            return;
        }

        using var key = await _privateContent.UnlockAsync(cancellationToken);
        var sealedContent = key.Seal(
            new SealedInventory(content.Name, content.Items),
            SealedContentSerializerContext.Default.SealedInventory);

        inventory.Name = string.Empty;
        // Blanked rather than sealed, as a task list's is - see LocalTaskListRepository.
        inventory.Description = string.Empty;
        inventory.Items = [];
        inventory.EncryptedCiphertext = sealedContent.Ciphertext;
        inventory.EncryptedNonce = sealedContent.Nonce;
    }

    public async Task<LocalWriteOutcome> DeleteAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Inventories.FirstOrDefaultAsync(
                candidate => candidate.LocalId == localId, cancellationToken) is not { } inventory)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (!OfflineEditPolicy.IsAllowed(inventory, _networkStatus))
        {
            return LocalWriteOutcome.RefusedWhileOffline;
        }

        dbContext.Inventories.Remove(inventory);

        if (inventory.ServerId is null)
        {
            dbContext.Outbox.RemoveRange(dbContext.Outbox.Where(
                entry => entry.EntityType == SyncEntityType.Inventory && entry.LocalId == localId));
        }
        else
        {
            Enqueue(dbContext, localId, OutboxOperation.Delete, _timeProvider.GetUtcNow(), inventory.ServerId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <inheritdoc cref="LocalNoteRepository.CopyForEditingAsync"/>
    public async Task<LocalInventory?> CopyForEditingAsync(Guid originalLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Inventories.FirstOrDefaultAsync(
                candidate => candidate.LocalId == originalLocalId, cancellationToken)
            is not { IsSealed: false, IsPrivate: false } original)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        var copy = new LocalInventory
        {
            LocalId = Guid.NewGuid(),
            ServerId = null,
            Name = original.Name,
            // The shelf items keep their ids - see ICopyReviewStore.KeepCopyAsync for why, and for
            // where they are given up again.
            Items = original.Items,
            CopyOfLocalId = original.LocalId,
            CopiedAtUtc = now,
            CopyBaseTitle = original.Name,
            CopyBaseLines = Describe(original.Items),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Inventories.Add(copy);
        CopiesForEditing.Announce(dbContext, CopyKind.Inventory, copy.LocalId, original.Name, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return copy;
    }

    public CopyKind Kind => CopyKind.Inventory;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CopyUnderReview>> GetCopiesAwaitingReviewAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await DescribeAllAsync(dbContext, CopiesForEditing.AwaitingReviewAsync<LocalInventory>, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CopyUnderReview>> GetKeptCopiesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await DescribeAllAsync(dbContext, CopiesForEditing.KeptAsync<LocalInventory>, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CopyUnderReview>> GetHistoryOfAsync(
        Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await DescribeAllAsync(
            dbContext,
            (context, token) => CopiesForEditing.HistoryOfAsync<LocalInventory>(context, localId, token),
            cancellationToken);
    }

    /// <inheritdoc cref="LocalNoteRepository.ApplyCopyAsync"/>
    public async Task<LocalWriteOutcome> ApplyCopyAsync(Guid copyLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await CopiesForEditing.FindCopyAsync<LocalInventory>(dbContext, copyLocalId, cancellationToken)
            is not { CopyOfLocalId: { } originalLocalId } copy)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (await dbContext.Inventories.FirstOrDefaultAsync(
                candidate => candidate.LocalId == originalLocalId, cancellationToken) is not { } original)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (!SharedItemAccess.AllowsEditing(original))
        {
            return LocalWriteOutcome.RefusedAsReadOnly;
        }

        if (!OfflineEditPolicy.IsAllowed(original, _networkStatus))
        {
            return LocalWriteOutcome.RefusedWhileOffline;
        }

        var now = _timeProvider.GetUtcNow();
        original.Name = copy.Name;
        original.Items = copy.Items;
        original.UpdatedAtUtc = now;
        Enqueue(dbContext, original.LocalId, OutboxOperation.Update, now, original.ServerId);

        CopiesForEditing.Remove(dbContext, copy, SyncEntityType.Inventory);
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <inheritdoc/>
    public async Task<LocalWriteOutcome> DiscardCopyAsync(Guid copyLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await CopiesForEditing.FindCopyAsync<LocalInventory>(dbContext, copyLocalId, cancellationToken) is not { } copy)
        {
            return LocalWriteOutcome.NotFound;
        }

        CopiesForEditing.Remove(dbContext, copy, SyncEntityType.Inventory);
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <inheritdoc/>
    public async Task<LocalWriteOutcome> KeepCopyAsync(Guid copyLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await CopiesForEditing.FindCopyAsync<LocalInventory>(dbContext, copyLocalId, cancellationToken) is not { } copy)
        {
            return LocalWriteOutcome.NotFound;
        }

        var now = _timeProvider.GetUtcNow();
        // Its shelf items stop being the original's the moment this becomes an inventory of its own -
        // an errand still pointing at one of them means the shelf it was always about.
        copy.Items = [.. copy.Items.Select(item => item with { Id = null })];
        copy.UpdatedAtUtc = now;
        CopiesForEditing.Keep(dbContext, copy, SyncEntityType.Inventory, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <inheritdoc cref="LocalNoteRepository.DescribeAllAsync"/>
    private async Task<IReadOnlyList<CopyUnderReview>> DescribeAllAsync(
        OrbitLocalDbContext dbContext,
        Func<OrbitLocalDbContext, CancellationToken, Task<IReadOnlyList<LocalInventory>>> read,
        CancellationToken cancellationToken)
    {
        var described = new List<CopyUnderReview>();
        foreach (var copy in await read(dbContext, cancellationToken))
        {
            var original = await dbContext.Inventories.AsNoTracking().FirstOrDefaultAsync(
                candidate => candidate.LocalId == copy.CopyOfLocalId, cancellationToken);

            described.Add(new CopyUnderReview(
                CopyKind.Inventory, copy.LocalId, copy.CopyOfLocalId!.Value,
                original?.Name is { Length: > 0 } name ? name : copy.CopyBaseTitle,
                copy.CopiedAtUtc ?? copy.CreatedAtUtc,
                copy.CopyBaseLines, Describe(copy.Items),
                original is null ? null : Describe(original.Items),
                copy.IsKeptCopy));
        }

        return described;
    }

    /// <summary>
    /// A shelf as a review reads it: what is on it, how much, and how little is too little. Amounts are
    /// written plainly and unlocalised - see <see cref="LocalTaskListRepository.Describe"/>.
    /// </summary>
    private static IReadOnlyList<string> Describe(IReadOnlyList<InventoryItemRequest> items)
        => [.. items.Select(item => item.MinimumQuantity is { } minimum
            ? $"{item.Name}: {item.Quantity} {item.Unit} (min {minimum})"
            : $"{item.Name}: {item.Quantity} {item.Unit}")];

    private static void Enqueue(
        OrbitLocalDbContext dbContext, Guid localId, OutboxOperation operation, DateTimeOffset queuedAtUtc,
        Guid? serverId = null)
        => dbContext.Outbox.Add(new OutboxEntry
        {
            EntityType = SyncEntityType.Inventory,
            LocalId = localId,
            ServerId = serverId,
            Operation = operation,
            QueuedAtUtc = queuedAtUtc
        });
}
