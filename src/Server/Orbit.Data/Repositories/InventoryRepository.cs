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

        return entities
            .OrderBy(inventory => inventory.Name)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<Inventory?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Inventories
            .AsNoTracking()
            .FirstOrDefaultAsync(inventory => inventory.Id == id && inventory.UserId == userId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(Inventory inventory, CancellationToken cancellationToken)
    {
        _dbContext.Inventories.Add(ToEntity(inventory));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Inventory inventory, CancellationToken cancellationToken)
    {
        _dbContext.Inventories.Update(ToEntity(inventory));
        await _dbContext.SaveChangesAsync(cancellationToken);
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

    private static Inventory ToDomain(InventoryEntity entity)
        => Inventory.FromPersistence(
            entity.Id, entity.UserId, entity.Name, entity.IsPrivate,
            ToEncryptedPayload(entity.EncryptedCiphertext, entity.EncryptedNonce),
            entity.CreatedAtUtc, entity.UpdatedAtUtc,
            entity.LockedByUserId, entity.LockedByUserName, entity.LockExpiresAtUtc, entity.Description);

    private static InventoryEntity ToEntity(Inventory inventory)
        => new()
        {
            Id = inventory.Id,
            UserId = inventory.UserId,
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
