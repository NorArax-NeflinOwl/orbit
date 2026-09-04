using Microsoft.EntityFrameworkCore;
using Orbit.Core.Inventories;
using Orbit.Core.Notifications;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class InventoryItemRepository : IInventoryItemRepository
{
    private readonly OrbitDbContext _dbContext;

    public InventoryItemRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<InventoryItem>> GetAllAsync(Guid inventoryId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.InventoryItems
            .AsNoTracking()
            .Include(entity => entity.Categories)
            .Where(entity => entity.InventoryId == inventoryId)
            .ToListAsync(cancellationToken);

        // As arranged, then alphabetically - which is the whole order for an inventory nobody has
        // arranged yet, since everything in one sits at position zero.
        return entities
            .OrderBy(entity => entity.Position)
            .ThenBy(entity => entity.Name)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<InventoryItem?> GetByIdAsync(Guid inventoryId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.InventoryItems
            .AsNoTracking()
            .Include(item => item.Categories)
            .FirstOrDefaultAsync(item => item.Id == id && item.InventoryId == inventoryId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(InventoryItem item, CancellationToken cancellationToken)
    {
        _dbContext.InventoryItems.Add(ToEntity(item));
        await MarkInventoryChangedAsync(item.InventoryId, cancellationToken);
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
        var entity = await _dbContext.InventoryItems
            .Include(existing => existing.Categories)
            .FirstAsync(existing => existing.Id == item.Id, cancellationToken);
        entity.Name = item.Name;
        entity.ProductType = item.ProductType;
        // Replaced rather than merged: a save is the whole list, and the rows carry nothing but the
        // words and their order, so there is no state to lose by rewriting them - unlike the item row
        // itself, which is why that one is updated in place. Mirrors TaskRepository's own categories.
        entity.Categories.Clear();
        entity.Categories.AddRange(ToCategoryEntities(item));
        entity.Quantity = item.Quantity;
        entity.MinimumQuantity = item.MinimumQuantity;
        entity.Unit = item.Unit.ToString();
        entity.ExpiryDate = item.ExpiryDate;
        entity.ExpiryNotificationChannel = item.ExpiryNotificationChannel.ToString();
        entity.PendingRestockTaskListId = item.PendingRestockTaskListId;
        entity.PendingRestockTaskItemId = item.PendingRestockTaskItemId;
        entity.Position = item.Position;
        entity.UpdatedAtUtc = item.UpdatedAtUtc;

        await MarkInventoryChangedAsync(item.InventoryId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid inventoryId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.InventoryItems
            .FirstOrDefaultAsync(item => item.Id == id && item.InventoryId == inventoryId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        _dbContext.InventoryItems.Remove(entity);
        await MarkInventoryChangedAsync(inventoryId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAllInInventoryAsync(Guid inventoryId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.InventoryItems
            .Where(item => item.InventoryId == inventoryId)
            .ToListAsync(cancellationToken);
        if (entities.Count == 0)
        {
            return;
        }

        _dbContext.InventoryItems.RemoveRange(entities);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Says the inventory changed, in the same write as the item that changed inside it.
    ///
    /// The change feed's unit is the inventory: its items travel inside it and are gated by its
    /// timestamp - see InventoryRepository.GetAllAsync. So an item written on its own never reached
    /// another device. Finishing a restock round brought the shelf up to its minimums on the server and
    /// left every phone showing what it last saw; the next save from one of them wrote that back over
    /// the top-up. Saving an inventory stamps it anyway, so this only matters for the writes that go
    /// straight at an item, which are all the server's own.
    /// </summary>
    private async Task MarkInventoryChangedAsync(Guid inventoryId, CancellationToken cancellationToken)
    {
        // Gone already when the whole inventory is being deleted, which is not a change to report.
        if (await _dbContext.Inventories.FirstOrDefaultAsync(
                inventory => inventory.Id == inventoryId, cancellationToken) is { } entity)
        {
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// The item's categories as rows, numbered in the order it holds them - see
    /// InventoryItemCategoryEntity.Position for why the order is stored rather than inferred.
    /// </summary>
    private static List<InventoryItemCategoryEntity> ToCategoryEntities(InventoryItem item)
        => [.. item.Categories.Select((category, position) => new InventoryItemCategoryEntity
        {
            InventoryItemId = item.Id,
            Category = category,
            Position = position
        })];

    private static InventoryItem ToDomain(InventoryItemEntity entity)
        => InventoryItem.FromPersistence(
            entity.Id, entity.InventoryId, entity.Name, entity.ProductType,
            [.. entity.Categories.OrderBy(category => category.Position).Select(category => category.Category)],
            entity.Quantity, entity.MinimumQuantity,
            Enum.Parse<InventoryUnit>(entity.Unit, ignoreCase: true), entity.ExpiryDate,
            Enum.Parse<NotificationChannel>(entity.ExpiryNotificationChannel, ignoreCase: true),
            entity.PendingRestockTaskListId, entity.PendingRestockTaskItemId, entity.Position, entity.CreatedAtUtc,
            entity.UpdatedAtUtc, entity.IsCheckedRegularly);

    private static InventoryItemEntity ToEntity(InventoryItem item)
        => new()
        {
            Id = item.Id,
            InventoryId = item.InventoryId,
            Name = item.Name,
            ProductType = item.ProductType,
            Categories = ToCategoryEntities(item),
            Quantity = item.Quantity,
            MinimumQuantity = item.MinimumQuantity,
            IsCheckedRegularly = item.IsCheckedRegularly,
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
