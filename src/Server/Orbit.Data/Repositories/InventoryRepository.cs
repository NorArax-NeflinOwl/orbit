using Microsoft.EntityFrameworkCore;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class InventoryRepository : IInventoryRepository
{
    private readonly OrbitDbContext _dbContext;

    public InventoryRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Inventory>> GetAllAsync(
        Guid userId, DateTimeOffset? updatedSinceUtc, CancellationToken cancellationToken)
    {
        var query = _dbContext.Inventories
            .AsNoTracking()
            .Where(inventory => inventory.UserId == userId);

        // Narrowed in the database when the caller only wants what changed. A client catching up asks
        // for a delta; fetching everything and dropping most of it here saved the wire and nothing else.
        if (updatedSinceUtc is not null)
        {
            query = query.Where(inventory => inventory.UpdatedAtUtc >= updatedSinceUtc.Value);
        }

        var entities = await query.ToListAsync(cancellationToken);
        var gathered = await GatheredByAsync([.. entities.Select(inventory => inventory.Id)], cancellationToken);

        return entities
            .OrderBy(inventory => inventory.Name)
            .Select(entity => ToDomain(entity, gathered))
            .ToList();
    }

    public async Task<Inventory?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Inventories
            .AsNoTracking()
            .FirstOrDefaultAsync(inventory => inventory.Id == id && inventory.UserId == userId, cancellationToken);

        return entity is null ? null : ToDomain(entity, await GatheredByAsync([id], cancellationToken));
    }

    public async Task AddAsync(Inventory inventory, CancellationToken cancellationToken)
    {
        _dbContext.Inventories.Add(ToEntity(inventory));
        WriteWhatItGathers(inventory);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Inventory inventory, CancellationToken cancellationToken)
    {
        _dbContext.Inventories.Update(ToEntity(inventory));
        // Read first, because the rows are replaced wholesale below and EF needs to be tracking what is
        // being removed. Only this group's - a shelf gathered by two groups has a row under each.
        var stored = await _dbContext.InventoriesGathered
            .Where(gathered => gathered.InventoryId == inventory.Id)
            .ToListAsync(cancellationToken);
        _dbContext.InventoriesGathered.RemoveRange(stored);
        WriteWhatItGathers(inventory);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The membership as the domain holds it: deleted and written again rather than reconciled, which is
    /// what makes the stored order the arranged one - see InventoryGatheredEntity.Position.
    /// </summary>
    private void WriteWhatItGathers(Inventory inventory)
    {
        foreach (var (gatheredId, position) in inventory.GathersInventoryIds.Select((id, at) => (id, at)))
        {
            _dbContext.InventoriesGathered.Add(new InventoryGatheredEntity
            {
                InventoryId = inventory.Id,
                GatheredInventoryId = gatheredId,
                Position = position
            });
        }
    }

    /// <summary>
    /// What each of these shelves gathers, in the order somebody arranged it. Read in one query for the
    /// whole page rather than one per shelf: the inventories list asks this of every row it draws.
    /// </summary>
    private async Task<ILookup<Guid, Guid>> GatheredByAsync(
        IReadOnlyList<Guid> inventoryIds, CancellationToken cancellationToken)
    {
        if (inventoryIds.Count == 0)
        {
            return Array.Empty<(Guid, Guid)>().ToLookup(pair => pair.Item1, pair => pair.Item2);
        }

        var rows = await _dbContext.InventoriesGathered
            .AsNoTracking()
            .Where(gathered => inventoryIds.Contains(gathered.InventoryId))
            .OrderBy(gathered => gathered.Position)
            .ToListAsync(cancellationToken);

        return rows.ToLookup(gathered => gathered.InventoryId, gathered => gathered.GatheredInventoryId);
    }

    /// <summary>The three columns a lock is, and nothing else - see IInventoryRepository.UpdateLockAsync.</summary>
    public async Task UpdateLockAsync(Inventory inventory, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Inventories.FirstAsync(stored => stored.Id == inventory.Id, cancellationToken);
        entity.LockedByUserId = inventory.LockedByUserId;
        entity.LockedByUserName = inventory.LockedByUserName;
        entity.LockExpiresAtUtc = inventory.LockExpiresAtUtc;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Inventories
            .FirstOrDefaultAsync(inventory => inventory.Id == id && inventory.UserId == userId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        _dbContext.Inventories.Remove(entity);
        // Both ends of any gathering it took part in. There is no foreign key here - a member that has
        // gone reads as "nothing there" when a group is walked, deliberately - so the rows are taken
        // away by hand, or a group would go on counting a shelf nobody can open and a deleted group
        // would leave its own list behind.
        var gathering = await _dbContext.InventoriesGathered
            .Where(gathered => gathered.InventoryId == id || gathered.GatheredInventoryId == id)
            .ToListAsync(cancellationToken);
        _dbContext.InventoriesGathered.RemoveRange(gathering);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid?> GetOwnerUserIdAsync(Guid inventoryId, CancellationToken cancellationToken)
    {
        var owner = await _dbContext.Inventories
            .AsNoTracking()
            .Where(inventory => inventory.Id == inventoryId)
            .Select(inventory => (Guid?)inventory.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        return owner;
    }

    /// <summary>Both columns are written together or not at all, so either alone means no sealed content.</summary>
    private static EncryptedPayload? ToEncryptedPayload(string? ciphertext, string? nonce)
        // Blank counts as absent, not just null: a row half-written before EncryptedPayload started
        // checking its own parts would otherwise fail inside that check while being read, which is the
        // one place a stored row must never throw.
        => !string.IsNullOrWhiteSpace(ciphertext) && !string.IsNullOrWhiteSpace(nonce)
            ? new EncryptedPayload(ciphertext, nonce)
            : null;

    private static Inventory ToDomain(InventoryEntity entity, ILookup<Guid, Guid> gathered)
        => Inventory.FromPersistence(
            entity.Id, entity.UserId, entity.Name, entity.IsPrivate,
            ToEncryptedPayload(entity.EncryptedCiphertext, entity.EncryptedNonce),
            entity.CreatedAtUtc, entity.UpdatedAtUtc,
            entity.LockedByUserId, entity.LockedByUserName, entity.LockExpiresAtUtc, entity.Description,
            entity.FolderId, entity.IsArchived, [.. gathered[entity.Id]]);

    private static InventoryEntity ToEntity(Inventory inventory)
        => new()
        {
            Id = inventory.Id,
            UserId = inventory.UserId,
            FolderId = inventory.FolderId,
            IsArchived = inventory.IsArchived,
            Name = inventory.Name,
            Description = inventory.Description,
            IsPrivate = inventory.IsPrivate,
            EncryptedCiphertext = inventory.EncryptedContent?.Ciphertext,
            EncryptedNonce = inventory.EncryptedContent?.Nonce,
            CreatedAtUtc = inventory.CreatedAtUtc,
            UpdatedAtUtc = inventory.UpdatedAtUtc,
            LockedByUserId = inventory.LockedByUserId,
            LockedByUserName = inventory.LockedByUserName,
            LockExpiresAtUtc = inventory.LockExpiresAtUtc
        };
}
