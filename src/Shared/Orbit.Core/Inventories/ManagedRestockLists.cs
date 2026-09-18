namespace Orbit.Core.Inventories;

/// <summary>
/// Which of a reader's task lists Orbit keeps for itself - the "Restock supplies" list an inventory
/// raises, rather than a list somebody wrote. See InventoryTaskListCoordinator, which makes them.
///
/// **Why anything asks.** A restock errand is a real task entry pointing at the shelf item it is about
/// (see EnsureRestockTaskAsync), so to anything reading "which entries stand for this shelf item" it
/// looks exactly like a list asking for it. That is wrong twice over: it added one to what the shelf
/// counts as wanted (ShelfUsage), pushing an item's kept level up by one for as long as its own restock
/// errand was open; and it makes every shelf row look asked-for by a second list, which is the case
/// ShelfDemand refuses to write without being told how to divide.
///
/// Orbit's own list is not a plan of the reader's. It is the consequence of one, and counting it is
/// counting the same want twice.
/// </summary>
public sealed class ManagedRestockLists(IInventoryManagedTaskListRepository managedTaskLists)
{
    /// <summary>
    /// The ones among <paramref name="taskListIds"/> that Orbit made. Asked per list rather than by
    /// fetching them all, because callers already know the handful of lists they care about - the ones
    /// holding an entry for a shelf item - and there is at most one such row per inventory.
    /// </summary>
    public async Task<IReadOnlySet<Guid>> AmongAsync(
        IEnumerable<Guid> taskListIds, CancellationToken cancellationToken)
    {
        var managed = new HashSet<Guid>();
        foreach (var taskListId in taskListIds.Distinct())
        {
            if (await managedTaskLists.GetInventoryIdAsync(taskListId, cancellationToken) is not null)
            {
                managed.Add(taskListId);
            }
        }

        return managed;
    }
}
