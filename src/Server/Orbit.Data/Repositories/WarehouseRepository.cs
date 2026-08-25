using Microsoft.EntityFrameworkCore;
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

    public async Task<IReadOnlyList<Warehouse>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.Warehouses
            .AsNoTracking()
            .Where(warehouse => warehouse.UserId == userId)
            .ToListAsync(cancellationToken);

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

    private static Warehouse ToDomain(WarehouseEntity entity)
        => Warehouse.FromPersistence(entity.Id, entity.UserId, entity.Name, entity.CreatedAtUtc, entity.UpdatedAtUtc);

    private static WarehouseEntity ToEntity(Warehouse warehouse)
        => new()
        {
            Id = warehouse.Id,
            UserId = warehouse.UserId,
            Name = warehouse.Name,
            CreatedAtUtc = warehouse.CreatedAtUtc,
            UpdatedAtUtc = warehouse.UpdatedAtUtc
        };
}
