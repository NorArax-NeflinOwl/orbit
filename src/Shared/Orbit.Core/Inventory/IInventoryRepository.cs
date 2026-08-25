namespace Orbit.Core.Inventory;

/// <summary>
/// Item lookups are scoped by warehouse, not by user - callers establish that the caller may touch that
/// warehouse first (see WarehouseAccessResolver) and pass its id here.
/// </summary>
public interface IInventoryRepository
{
    Task<IReadOnlyList<InventoryItem>> GetAllAsync(Guid warehouseId, CancellationToken cancellationToken);

    Task<InventoryItem?> GetByIdAsync(Guid warehouseId, Guid id, CancellationToken cancellationToken);

    Task AddAsync(InventoryItem item, CancellationToken cancellationToken);

    Task UpdateAsync(InventoryItem item, CancellationToken cancellationToken);

    Task DeleteAsync(Guid warehouseId, Guid id, CancellationToken cancellationToken);

    /// <summary>Removes every item in a warehouse, for when the warehouse itself is deleted.</summary>
    Task DeleteAllInWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken);
}
