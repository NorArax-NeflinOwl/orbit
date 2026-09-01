using Microsoft.EntityFrameworkCore;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class WarehouseRepository : IWarehouseRepository
{
    private readonly OrbitDbContext _dbContext;

    public WarehouseRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Warehouse>> GetAllAsync(
        Guid userId, DateTimeOffset? updatedSinceUtc, CancellationToken cancellationToken)
    {
        var query = _dbContext.Warehouses
            .AsNoTracking()
            .Where(warehouse => warehouse.UserId == userId);

        // Narrowed in the database when the caller only wants what changed. A client catching up asks
        // for a delta; fetching everything and dropping most of it here saved the wire and nothing else.
        if (updatedSinceUtc is not null)
        {
            query = query.Where(warehouse => warehouse.UpdatedAtUtc >= updatedSinceUtc.Value);
        }

        var entities = await query.ToListAsync(cancellationToken);

        return entities
            .OrderBy(warehouse => warehouse.Name)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<Warehouse?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(warehouse => warehouse.Id == id && warehouse.UserId == userId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken)
    {
        _dbContext.Warehouses.Add(ToEntity(warehouse));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Warehouse warehouse, CancellationToken cancellationToken)
    {
        _dbContext.Warehouses.Update(ToEntity(warehouse));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Warehouses
            .FirstOrDefaultAsync(warehouse => warehouse.Id == id && warehouse.UserId == userId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        _dbContext.Warehouses.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid?> GetOwnerUserIdAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        var owner = await _dbContext.Warehouses
            .AsNoTracking()
            .Where(warehouse => warehouse.Id == warehouseId)
            .Select(warehouse => (Guid?)warehouse.UserId)
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

    private static Warehouse ToDomain(WarehouseEntity entity)
        => Warehouse.FromPersistence(
            entity.Id, entity.UserId, entity.Name, entity.IsPrivate,
            ToEncryptedPayload(entity.EncryptedCiphertext, entity.EncryptedNonce),
            entity.CreatedAtUtc, entity.UpdatedAtUtc,
            entity.LockedByUserId, entity.LockedByUserName, entity.LockExpiresAtUtc, entity.Description);

    private static WarehouseEntity ToEntity(Warehouse warehouse)
        => new()
        {
            Id = warehouse.Id,
            UserId = warehouse.UserId,
            Name = warehouse.Name,
            Description = warehouse.Description,
            IsPrivate = warehouse.IsPrivate,
            EncryptedCiphertext = warehouse.EncryptedContent?.Ciphertext,
            EncryptedNonce = warehouse.EncryptedContent?.Nonce,
            CreatedAtUtc = warehouse.CreatedAtUtc,
            UpdatedAtUtc = warehouse.UpdatedAtUtc,
            LockedByUserId = warehouse.LockedByUserId,
            LockedByUserName = warehouse.LockedByUserName,
            LockExpiresAtUtc = warehouse.LockExpiresAtUtc
        };
}
