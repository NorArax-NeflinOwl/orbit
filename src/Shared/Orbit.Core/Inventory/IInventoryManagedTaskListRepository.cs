namespace Orbit.Core.Inventory;

/// <summary>
/// Tracks which TaskList (see Orbit.Core.Tasks) a given warehouse's Inventory feature has created for
/// itself - one row per warehouse, at most. Kept entirely separate from the Tasks domain/schema rather
/// than adding a "system-managed" flag to TaskList itself, so Inventory stays purely additive.
/// </summary>
public interface IInventoryManagedTaskListRepository
{
    Task<Guid?> GetTaskListIdAsync(Guid warehouseId, CancellationToken cancellationToken);

    /// <summary>
    /// The warehouse a task list was created for, or null for an ordinary list. Asked the other way
    /// round by the task side, which sees a list being saved and has to know whether finishing an entry
    /// on it means anything to a shelf.
    /// </summary>
    Task<Guid?> GetWarehouseIdAsync(Guid taskListId, CancellationToken cancellationToken);

    /// <summary>Inserts or replaces the tracked TaskListId for warehouseId - replacing matters when a previously tracked list was deleted and a fresh one had to be created.</summary>
    Task SetTaskListIdAsync(Guid warehouseId, Guid taskListId, CancellationToken cancellationToken);
}
