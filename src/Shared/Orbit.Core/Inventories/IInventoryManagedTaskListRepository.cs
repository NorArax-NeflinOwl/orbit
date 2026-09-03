namespace Orbit.Core.Inventories;

/// <summary>
/// Tracks which TaskList (see Orbit.Core.Tasks) a given inventory's Inventory feature has created for
/// itself - one row per inventory, at most. Kept entirely separate from the Tasks domain/schema rather
/// than adding a "system-managed" flag to TaskList itself, so Inventory stays purely additive.
/// </summary>
public interface IInventoryManagedTaskListRepository
{
    Task<Guid?> GetTaskListIdAsync(Guid inventoryId, CancellationToken cancellationToken);

    /// <summary>
    /// The inventory a task list was created for, or null for an ordinary list. Asked the other way
    /// round by the task side, which sees a list being saved and has to know whether finishing an entry
    /// on it means anything to a shelf.
    /// </summary>
    Task<Guid?> GetInventoryIdAsync(Guid taskListId, CancellationToken cancellationToken);

    /// <summary>Inserts or replaces the tracked TaskListId for inventoryId - replacing matters when a previously tracked list was deleted and a fresh one had to be created.</summary>
    Task SetTaskListIdAsync(Guid inventoryId, Guid taskListId, CancellationToken cancellationToken);

    /// <summary>
    /// How this inventory's restock list is built and when it comes round - see
    /// <see cref="RestockListSettings"/>. Answers the default for an inventory that has never had a list,
    /// so callers never have to tell "not set" from "set to the default".
    /// </summary>
    Task<RestockListSettings> GetSettingsAsync(Guid inventoryId, CancellationToken cancellationToken);

    Task SetSettingsAsync(Guid inventoryId, RestockListSettings settings, CancellationToken cancellationToken);
}
