using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;

namespace Orbit.Core.Inventory;

/// <summary>
/// Creates and maintains the single, system-managed TaskList ("Restock supplies") that Inventory uses
/// for both the standing "keep your stock updated" reminder and every per-product restock task - one
/// such list per warehouse, so two warehouses don't pile their restock tasks into the same list. See
/// IInventoryManagedTaskListRepository for why this is tracked outside the Tasks schema entirely.
///
/// The list itself belongs to the warehouse's *owner*, since a TaskList is owned by a user - this looks
/// that owner up itself rather than making every caller pass it alongside the warehouse id.
/// </summary>
public sealed class InventoryTaskListCoordinator
{
    /// <summary>Title of the system-managed task list this coordinator creates/reuses per warehouse.</summary>
    public const string ManagedTaskListTitle = "Restock supplies";

    /// <summary>
    /// Description of the standing, never-recreated reminder task - RemindDaily nags about it every day
    /// until the user checks it off, and unchecking it re-arms that daily nag. This is the "recurring
    /// reminder to keep stock updated" the feature asked for; Tasks has no recurrence engine to build a
    /// self-recreating task on top of, and RemindDaily already covers the same intent without one.
    /// </summary>
    public const string UpdateStockReminderDescription = "Update stock levels";

    private readonly ITaskRepository _taskRepository;
    private readonly IInventoryManagedTaskListRepository _managedTaskListRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly PendingRestockTaskResolver _pendingRestockTaskResolver;

    public InventoryTaskListCoordinator(
        ITaskRepository taskRepository, IInventoryManagedTaskListRepository managedTaskListRepository,
        IWarehouseRepository warehouseRepository, PendingRestockTaskResolver pendingRestockTaskResolver)
    {
        _taskRepository = taskRepository;
        _managedTaskListRepository = managedTaskListRepository;
        _warehouseRepository = warehouseRepository;
        _pendingRestockTaskResolver = pendingRestockTaskResolver;
    }

    /// <summary>
    /// Ensures warehouseId has a managed TaskList (creating it, with the standing reminder item, the
    /// first time it's needed) and returns its id. Also re-creates it if the previously tracked list was
    /// deleted out from under this tracking - the missing-list case is treated the same as never having
    /// had one, not an error. Returns null when the warehouse itself is gone.
    /// </summary>
    public async Task<Guid?> EnsureManagedTaskListAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        if (await _warehouseRepository.GetOwnerUserIdAsync(warehouseId, cancellationToken) is not { } ownerUserId)
        {
            return null;
        }

        var trackedTaskListId = await _managedTaskListRepository.GetTaskListIdAsync(warehouseId, cancellationToken);
        if (trackedTaskListId is { } existingId)
        {
            var existingTaskList = await _taskRepository.GetByIdAsync(ownerUserId, existingId, cancellationToken);
            if (existingTaskList is not null)
            {
                return existingId;
            }
        }

        var reminderItem = TaskItem.Create(
            UpdateStockReminderDescription, dueDateUtc: null, isCompleted: false,
            remindDaily: true, dailyReminderNotificationChannel: NotificationChannel.Push);
        // Pinned from the moment it exists: this is the one list Orbit maintains rather than the reader,
        // and it is only useful if it is where they will see it.
        var taskList = TaskList.Create(ownerUserId, ManagedTaskListTitle, [reminderItem], isPinned: true);
        await _taskRepository.AddAsync(taskList, cancellationToken);
        await _managedTaskListRepository.SetTaskListIdAsync(warehouseId, taskList.Id, cancellationToken);
        return taskList.Id;
    }

    /// <summary>
    /// Resolves item's pending-task state, then - if it's below minimum and nothing is already open -
    /// appends a fresh restock TaskItem to its warehouse's managed list. Returns the (possibly mutated)
    /// item; callers are responsible for persisting it if <see cref="InventoryItem.PendingRestockTaskListId"/>
    /// changed. A no-op, beyond the resolve step, when item isn't below minimum or already has an open
    /// restock task.
    /// </summary>
    public async Task<InventoryItem> EnsureRestockTaskAsync(InventoryItem item, CancellationToken cancellationToken)
    {
        item = await _pendingRestockTaskResolver.ResolveAsync(item, cancellationToken);
        if (!item.IsBelowMinimum || item.PendingRestockTaskItemId is not null)
        {
            return item;
        }

        if (await EnsureManagedTaskListAsync(item.WarehouseId, cancellationToken) is not { } taskListId)
        {
            return item;
        }

        var ownerUserId = await _warehouseRepository.GetOwnerUserIdAsync(item.WarehouseId, cancellationToken)
            ?? throw new InvalidOperationException($"Warehouse {item.WarehouseId} disappeared between ensuring its task list and using it.");
        var taskList = await _taskRepository.GetByIdAsync(ownerUserId, taskListId, cancellationToken)
            ?? throw new InvalidOperationException($"Managed task list {taskListId} for warehouse {item.WarehouseId} disappeared between ensuring it and using it.");

        var restockItem = TaskItem.Create($"Restock: {item.Name}", dueDateUtc: null, isCompleted: false);
        if (taskList.IsPrivate)
        {
            // The list's items are sealed in the owner's browser, so appending here would either be
            // invisible or - since a private list keeps no readable items - quietly dropped. Better to
            // say so than to leave the warehouse looking like it raised a restock task it didn't.
            throw new InvalidRequestException(
                $"The restock list for this warehouse is private, so Orbit can't add \"{item.Name}\" to it. Turn privacy off for that list first.");
        }

        taskList.Update(taskList.Title, [.. taskList.Items, restockItem], taskList.IsGroup, taskList.IsPrivate, taskList.EncryptedContent);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);

        item.SetPendingRestockTask(taskListId, restockItem.Id);
        return item;
    }
}
