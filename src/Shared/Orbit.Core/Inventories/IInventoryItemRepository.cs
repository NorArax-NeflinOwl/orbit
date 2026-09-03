namespace Orbit.Core.Inventories;

/// <summary>
/// Item lookups are scoped by inventory, not by user - callers establish that the caller may touch that
/// inventory first (see InventoryAccessResolver) and pass its id here.
/// </summary>
public interface IInventoryItemRepository
{
    Task<IReadOnlyList<InventoryItem>> GetAllAsync(Guid inventoryId, CancellationToken cancellationToken);

    Task<InventoryItem?> GetByIdAsync(Guid inventoryId, Guid id, CancellationToken cancellationToken);

    Task AddAsync(InventoryItem item, CancellationToken cancellationToken);

    Task UpdateAsync(InventoryItem item, CancellationToken cancellationToken);

    Task DeleteAsync(Guid inventoryId, Guid id, CancellationToken cancellationToken);

    /// <summary>Removes every item in an inventory, for when the inventory itself is deleted.</summary>
    Task DeleteAllInInventoryAsync(Guid inventoryId, CancellationToken cancellationToken);
}
