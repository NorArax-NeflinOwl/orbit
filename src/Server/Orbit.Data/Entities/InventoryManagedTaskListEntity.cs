namespace Orbit.Data.Entities;

/// <summary>
/// Tracks the single system-managed TaskList a given user's Inventory feature created for itself - see
/// Orbit.Core.Inventory.IInventoryManagedTaskListRepository. One row per user, unique on UserId.
/// </summary>
public sealed class InventoryManagedTaskListEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TaskListId { get; set; }
}
