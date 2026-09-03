using Orbit.Core.Tasks;

namespace Orbit.Core.Inventories;

/// <summary>
/// Notices when an item's tracked restock task (see InventoryItem.PendingRestockTaskListId/
/// PendingRestockTaskItemId) has been deleted out from under it - the list or the entry itself is gone -
/// so a new one can be created next time this item goes low again.
///
/// Completing the task does not count as losing it. It used to: the link was dropped the moment the
/// reader ticked the task off, so the next save appended a second "Restock: X" beside the finished one,
/// and a third the day after that. The entry stays this item's entry, and is reopened rather than
/// duplicated - see InventoryTaskListCoordinator.EnsureRestockTaskAsync. Mirrors
/// LinkedTaskCompletionResolver's philosophy of treating a dangling reference as "not blocking" rather
/// than failing, but without that resolver's transitive/cyclic complexity, since a restock reference
/// only ever points at one concrete TaskItem, never a chain.
/// </summary>
public sealed class PendingRestockTaskResolver
{
    private readonly ITaskRepository _taskRepository;
    private readonly IInventoryRepository _inventoryRepository;

    public PendingRestockTaskResolver(ITaskRepository taskRepository, IInventoryRepository inventoryRepository)
    {
        _taskRepository = taskRepository;
        _inventoryRepository = inventoryRepository;
    }

    /// <summary>
    /// Returns item unchanged if it has no restock task tracked, or if the tracked one still exists -
    /// finished or not. Clears the tracking fields only when the list or the entry is gone; callers are
    /// responsible for persisting the returned item if it differs from the one passed in.
    /// </summary>
    public async Task<InventoryItem> ResolveAsync(InventoryItem item, CancellationToken cancellationToken)
    {
        if (item.PendingRestockTaskListId is not { } taskListId || item.PendingRestockTaskItemId is not { } taskItemId)
        {
            return item;
        }

        // The managed task list belongs to the inventory's owner, not to whoever is currently looking
        // at the item - a share recipient resolving this must still find the owner's list.
        var ownerUserId = await _inventoryRepository.GetOwnerUserIdAsync(item.InventoryId, cancellationToken);
        var taskList = ownerUserId is null ? null : await _taskRepository.GetByIdAsync(ownerUserId.Value, taskListId, cancellationToken);
        var taskItem = taskList?.Items.FirstOrDefault(candidate => candidate.Id == taskItemId);
        if (taskItem is null)
        {
            item.ClearPendingRestockTask();
        }

        return item;
    }
}
