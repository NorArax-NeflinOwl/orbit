using Microsoft.EntityFrameworkCore;
using Orbit.Core.Inventory;
using Orbit.Core.Notifications;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class InventoryRepository : IInventoryRepository
{
    private readonly OrbitDbContext _dbContext;

    public InventoryRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<InventoryItem>> GetAllAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.InventoryItems
            .AsNoTracking()
            .Where(entity => entity.WarehouseId == warehouseId)
            .ToListAsync(cancellationToken);

        // As arranged, then alphabetically - which is the whole order for a warehouse nobody has
        // arranged yet, since everything in one sits at position zero.
        return entities
            .OrderBy(entity => entity.Position)
            .ThenBy(entity => entity.Name)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<InventoryItem?> GetByIdAsync(Guid warehouseId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.InventoryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.WarehouseId == warehouseId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(InventoryItem item, CancellationToken cancellationToken)
    {
        _dbContext.InventoryItems.Add(ToEntity(item));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(InventoryItem item, CancellationToken cancellationToken)
    {
        // Updates the tracked entity's properties in place rather than attaching a fresh ToEntity(item)
        // instance (as NoteRepository/TaskRepository's own UpdateAsync do) - CreateInventoryItemCommandHandler
        // can call AddAsync and then, within the same request/DbContext, UpdateAsync on the very same
        // item (see InventoryTaskListCoordinator.EnsureRestockTaskAsync), and attaching a second entity
        // instance with the same key the context already tracks throws
        // "another instance with the same key value is already being tracked".
        var entity = await _dbContext.InventoryItems.FirstAsync(existing => existing.Id == item.Id, cancellationToken);
        entity.Name = item.Name;
        entity.ProductType = item.ProductType;
        entity.Category = item.Category;
        entity.Quantity = item.Quantity;
        entity.MinimumQuantity = item.MinimumQuantity;
        entity.Unit = item.Unit.ToString();
        entity.ExpiryDate = item.ExpiryDate;
        entity.ExpiryNotificationChannel = item.ExpiryNotificationChannel.ToString();
        entity.PendingRestockTaskListId = item.PendingRestockTaskListId;
        entity.PendingRestockTaskItemId = item.PendingRestockTaskItemId;
        entity.Position = item.Position;
        entity.UpdatedAtUtc = item.UpdatedAtUtc;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid warehouseId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.InventoryItems
            .FirstOrDefaultAsync(item => item.Id == id && item.WarehouseId == warehouseId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        _dbContext.InventoryItems.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAllInWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.InventoryItems
            .Where(item => item.WarehouseId == warehouseId)
            .ToListAsync(cancellationToken);
        if (entities.Count == 0)
        {
            return;
        }

        _dbContext.InventoryItems.RemoveRange(entities);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static InventoryItem ToDomain(InventoryItemEntity entity)
        => InventoryItem.FromPersistence(
            entity.Id, entity.WarehouseId, entity.Name, entity.ProductType, entity.Category, entity.Quantity, entity.MinimumQuantity,
            Enum.Parse<InventoryUnit>(entity.Unit, ignoreCase: true), entity.ExpiryDate,
            Enum.Parse<NotificationChannel>(entity.ExpiryNotificationChannel, ignoreCase: true),
            entity.PendingRestockTaskListId, entity.PendingRestockTaskItemId, entity.Position, entity.CreatedAtUtc,
            entity.UpdatedAtUtc);

    private static InventoryItemEntity ToEntity(InventoryItem item)
        => new()
        {
            Id = item.Id,
            WarehouseId = item.WarehouseId,
            Name = item.Name,
            ProductType = item.ProductType,
            Category = item.Category,
            Quantity = item.Quantity,
            MinimumQuantity = item.MinimumQuantity,
            Unit = item.Unit.ToString(),
            ExpiryDate = item.ExpiryDate,
            ExpiryNotificationChannel = item.ExpiryNotificationChannel.ToString(),
            PendingRestockTaskListId = item.PendingRestockTaskListId,
            PendingRestockTaskItemId = item.PendingRestockTaskItemId,
            Position = item.Position,
            CreatedAtUtc = item.CreatedAtUtc,
            UpdatedAtUtc = item.UpdatedAtUtc
        };
}
