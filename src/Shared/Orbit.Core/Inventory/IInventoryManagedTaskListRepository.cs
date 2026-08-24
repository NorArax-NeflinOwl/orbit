namespace Orbit.Core.Inventory;

/// <summary>
/// Tracks which TaskList (see Orbit.Core.Tasks) a given user's Inventory feature has created for
/// itself - one row per user, at most. Kept entirely separate from the Tasks domain/schema rather than
/// adding a "system-managed" flag to TaskList itself, so Inventory stays purely additive.
/// </summary>
public interface IInventoryManagedTaskListRepository
{
    Task<Guid?> GetTaskListIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Inserts or replaces the tracked TaskListId for userId - replacing matters when a previously tracked list was deleted and a fresh one had to be created.</summary>
    Task SetTaskListIdAsync(Guid userId, Guid taskListId, CancellationToken cancellationToken);
}
