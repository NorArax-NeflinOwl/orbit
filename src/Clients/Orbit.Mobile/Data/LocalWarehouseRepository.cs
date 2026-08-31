using Microsoft.EntityFrameworkCore;
using Orbit.Contracts.Inventory;
using Orbit.Core.Sync;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Data;

/// <summary>
/// A warehouse as the reader has just left it. The same shape as <see cref="NoteContent"/> and
/// <see cref="TaskListContent"/>, for the same reason: the three repositories are written alike, so a
/// change made to one is obvious in the others.
/// </summary>
/// <param name="IsPrivate"><inheritdoc cref="NoteContent.IsPrivate" path="/summary"/></param>
public sealed record WarehouseContent(
    string Name, IReadOnlyList<WarehouseItemDto> Items, bool IsPrivate = false);

/// <summary>
/// Every read and write a screen performs on warehouses - the same shape as the other three, including
/// the rule that each write records its own outbox entry in the same transaction as the change, and that
/// the offline policy is refused here rather than only shown on screen.
/// </summary>
public sealed class LocalWarehouseRepository : ICopyReviewStore
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly INetworkStatus _networkStatus;
    private readonly PrivateContentSealer _privateContent;

    public LocalWarehouseRepository(
        IDbContextFactory<OrbitLocalDbContext> dbContextFactory, TimeProvider timeProvider, INetworkStatus networkStatus,
        PrivateContentSealer privateContent)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
        _networkStatus = networkStatus;
        _privateContent = privateContent;
    }

    public async Task<IReadOnlyList<LocalWarehouse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var warehouses = await dbContext.Warehouses
            .AsNoTracking()
            .OrderByDescending(warehouse => warehouse.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        await OpenPrivateContentAsync(warehouses, cancellationToken);
        return warehouses;
    }

    public async Task<LocalWarehouse?> FindAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var warehouse = await dbContext.Warehouses.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.LocalId == localId, cancellationToken);

        if (warehouse is not null)
        {
            await OpenPrivateContentAsync([warehouse], cancellationToken);
        }

        return warehouse;
    }

    /// <inheritdoc cref="LocalNoteRepository.OpenPrivateContentAsync"/>
    private async Task OpenPrivateContentAsync(
        IReadOnlyList<LocalWarehouse> warehouses, CancellationToken cancellationToken)
    {
        var privateWarehouses = warehouses.Where(warehouse => warehouse.IsPrivate).ToList();
        if (privateWarehouses.Count == 0)
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
            foreach (var warehouse in privateWarehouses)
            {
                warehouse.IsSealed = true;
            }

            return;
        }

        using (key)
        {
            foreach (var warehouse in privateWarehouses)
            {
                Open(key, warehouse);
            }
        }
    }

    private static void Open(PrivateContentKey key, LocalWarehouse warehouse)
    {
        if (warehouse.EncryptedContent is not { } encryptedContent
            || key.Open(encryptedContent, SealedContentSerializerContext.Default.SealedWarehouse) is not { } opened)
        {
            warehouse.IsSealed = true;
            return;
        }

        warehouse.Name = opened.Name;
        warehouse.Items = opened.Items;
        warehouse.IsSealed = false;
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
        Guid localId, WarehouseContent content, CancellationToken cancellationToken = default)
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
        await WriteContentAsync(warehouse, content, cancellationToken);
        warehouse.UpdatedAtUtc = now;

        // A copy still awaiting review is written to this phone and queued for nobody: what it is has
        // not been decided yet, and the review is what sends it - see LocalNoteRepository.UpdateAsync.
        if (!CopiesForEditing.IsAwaitingReview(warehouse))
        {
            Enqueue(dbContext, localId, OutboxOperation.Update, now);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <inheritdoc cref="LocalNoteRepository.WriteContentAsync"/>
    private async Task WriteContentAsync(
        LocalWarehouse warehouse, WarehouseContent content, CancellationToken cancellationToken)
    {
        warehouse.IsPrivate = content.IsPrivate;
        warehouse.IsSealed = false;

        if (!content.IsPrivate)
        {
            warehouse.Name = content.Name;
            warehouse.Items = content.Items;
            warehouse.EncryptedCiphertext = null;
            warehouse.EncryptedNonce = null;
            return;
        }

        using var key = await _privateContent.UnlockAsync(cancellationToken);
        var sealedContent = key.Seal(
            new SealedWarehouse(content.Name, content.Items),
            SealedContentSerializerContext.Default.SealedWarehouse);

        warehouse.Name = string.Empty;
        warehouse.Items = [];
        warehouse.EncryptedCiphertext = sealedContent.Ciphertext;
        warehouse.EncryptedNonce = sealedContent.Nonce;
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

    /// <inheritdoc cref="LocalNoteRepository.CopyForEditingAsync"/>
    public async Task<LocalWarehouse?> CopyForEditingAsync(Guid originalLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Warehouses.FirstOrDefaultAsync(
                candidate => candidate.LocalId == originalLocalId, cancellationToken)
            is not { IsSealed: false, IsPrivate: false } original)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        var copy = new LocalWarehouse
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

        dbContext.Warehouses.Add(copy);
        await dbContext.SaveChangesAsync(cancellationToken);
        return copy;
    }

    public CopyKind Kind => CopyKind.Warehouse;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CopyUnderReview>> GetCopiesAwaitingReviewAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await DescribeAllAsync(dbContext, CopiesForEditing.AwaitingReviewAsync<LocalWarehouse>, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CopyUnderReview>> GetKeptCopiesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await DescribeAllAsync(dbContext, CopiesForEditing.KeptAsync<LocalWarehouse>, cancellationToken);
    }

    /// <inheritdoc cref="LocalNoteRepository.ApplyCopyAsync"/>
    public async Task<LocalWriteOutcome> ApplyCopyAsync(Guid copyLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await CopiesForEditing.FindCopyAsync<LocalWarehouse>(dbContext, copyLocalId, cancellationToken)
            is not { CopyOfLocalId: { } originalLocalId } copy)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (await dbContext.Warehouses.FirstOrDefaultAsync(
                candidate => candidate.LocalId == originalLocalId, cancellationToken) is not { } original)
        {
            return LocalWriteOutcome.NotFound;
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

        CopiesForEditing.Remove(dbContext, copy, SyncEntityType.Warehouse);
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <inheritdoc/>
    public async Task<LocalWriteOutcome> DiscardCopyAsync(Guid copyLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await CopiesForEditing.FindCopyAsync<LocalWarehouse>(dbContext, copyLocalId, cancellationToken) is not { } copy)
        {
            return LocalWriteOutcome.NotFound;
        }

        CopiesForEditing.Remove(dbContext, copy, SyncEntityType.Warehouse);
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <inheritdoc/>
    public async Task<LocalWriteOutcome> KeepCopyAsync(Guid copyLocalId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await CopiesForEditing.FindCopyAsync<LocalWarehouse>(dbContext, copyLocalId, cancellationToken) is not { } copy)
        {
            return LocalWriteOutcome.NotFound;
        }

        var now = _timeProvider.GetUtcNow();
        // Its shelf items stop being the original's the moment this becomes a warehouse of its own -
        // an errand still pointing at one of them means the shelf it was always about.
        copy.Items = [.. copy.Items.Select(item => item with { Id = null })];
        copy.UpdatedAtUtc = now;
        CopiesForEditing.Keep(dbContext, copy, SyncEntityType.Warehouse, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <inheritdoc cref="LocalNoteRepository.DescribeAllAsync"/>
    private async Task<IReadOnlyList<CopyUnderReview>> DescribeAllAsync(
        OrbitLocalDbContext dbContext,
        Func<OrbitLocalDbContext, CancellationToken, Task<IReadOnlyList<LocalWarehouse>>> read,
        CancellationToken cancellationToken)
    {
        var described = new List<CopyUnderReview>();
        foreach (var copy in await read(dbContext, cancellationToken))
        {
            var original = await dbContext.Warehouses.AsNoTracking().FirstOrDefaultAsync(
                candidate => candidate.LocalId == copy.CopyOfLocalId, cancellationToken);

            described.Add(new CopyUnderReview(
                CopyKind.Warehouse, copy.LocalId, copy.CopyOfLocalId!.Value,
                original?.Name is { Length: > 0 } name ? name : copy.CopyBaseTitle,
                copy.CopiedAtUtc ?? copy.CreatedAtUtc,
                copy.CopyBaseLines, Describe(copy.Items),
                original is null ? null : Describe(original.Items)));
        }

        return described;
    }

    /// <summary>
    /// A shelf as a review reads it: what is on it, how much, and how little is too little. Amounts are
    /// written plainly and unlocalised - see <see cref="LocalTaskListRepository.Describe"/>.
    /// </summary>
    private static IReadOnlyList<string> Describe(IReadOnlyList<WarehouseItemDto> items)
        => [.. items.Select(item => item.MinimumQuantity is { } minimum
            ? $"{item.Name}: {item.Quantity} {item.Unit} (min {minimum})"
            : $"{item.Name}: {item.Quantity} {item.Unit}")];

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
