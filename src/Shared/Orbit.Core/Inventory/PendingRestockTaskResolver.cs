using Orbit.Core.Tasks;

namespace Orbit.Core.Inventory;

/// <summary>
/// Notices when an item's tracked restock task (see InventoryItem.PendingRestockTaskListId/
/// PendingRestockTaskItemId) is no longer actually pending - because the user completed it, or deleted
/// the list/item it lived in - so a new one can be created next time this item goes low again. Mirrors
/// LinkedTaskCompletionResolver's philosophy of treating a dangling reference as "not blocking" rather
/// than failing, but without that resolver's transitive/cyclic complexity, since a restock reference
/// only ever points at one concrete TaskItem, never a chain.
/// </summary>
public sealed class PendingRestockTaskResolver
{
    private readonly ITaskRepository _taskRepository;
    private readonly IWarehouseRepository _warehouseRepository;

    public PendingRestockTaskResolver(ITaskRepository taskRepository, IWarehouseRepository warehouseRepository)
    {
        _taskRepository = taskRepository;
        _warehouseRepository = warehouseRepository;
    }

    /// <summary>
    /// Returns item unchanged if it has no pending restock task tracked, or if the tracked one is still
    /// open. Returns a copy with the pending-task fields cleared if the tracked list/item is missing, or
    /// the item has been completed - callers are responsible for persisting the returned item if it
    /// differs from the one passed in.
    /// </summary>
    public async Task<InventoryItem> ResolveAsync(InventoryItem item, CancellationToken cancellationToken)
    {
        if (item.PendingRestockTaskListId is not { } taskListId || item.PendingRestockTaskItemId is not { } taskItemId)
        {
            return item;
        }

        // The managed task list belongs to the warehouse's owner, not to whoever is currently looking
        // at the item - a share recipient resolving this must still find the owner's list.
        var ownerUserId = await _warehouseRepository.GetOwnerUserIdAsync(item.WarehouseId, cancellationToken);
        var taskList = ownerUserId is null ? null : await _taskRepository.GetByIdAsync(ownerUserId.Value, taskListId, cancellationToken);
        var taskItem = taskList?.Items.FirstOrDefault(candidate => candidate.Id == taskItemId);
        if (taskItem is null || taskItem.IsCompleted)
        {
            item.ClearPendingRestockTask();
        }

        return item;
    }
}
