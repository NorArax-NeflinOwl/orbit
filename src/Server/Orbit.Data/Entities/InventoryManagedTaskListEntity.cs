namespace Orbit.Data.Entities;

/// <summary>
/// Tracks the single system-managed TaskList a given warehouse's Inventory feature created for itself -
/// see Orbit.Core.Inventory.IInventoryManagedTaskListRepository. One row per warehouse, unique on
/// WarehouseId.
/// </summary>
public sealed class InventoryManagedTaskListEntity
{
    public Guid Id { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid TaskListId { get; set; }
}
