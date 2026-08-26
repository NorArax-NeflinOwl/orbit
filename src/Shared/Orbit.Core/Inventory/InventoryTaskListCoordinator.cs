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
    /// Description of the standing, never-recreated reminder task. RemindDaily brings it back every day
    /// at its own time of day, whether or not the reader ticked it off yesterday - which is what makes
    /// one task enough, instead of a new one appearing each morning. Tasks has no recurrence engine to
    /// build a self-recreating task on top of, and this covers the same intent without one.
    /// </summary>
    public const string UpdateStockReminderDescription = "Update stock levels";

    /// <summary>
    /// When the standing reminder comes back and says so. Morning rather than the midnight a bare
    /// TimeOnly would default to - a stock reminder arriving while everyone is asleep is one nobody
    /// acts on. The reader can move it: it is an ordinary daily-reminder time on an ordinary task.
    /// </summary>
    private static readonly TimeOnly UpdateStockReminderTimeOfDay = new(9, 0);

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
            remindDaily: true, dailyReminderNotificationChannel: NotificationChannel.Push,
            dailyReminderTimeOfDay: UpdateStockReminderTimeOfDay);
        // Pinned from the moment it exists: this is the one list Orbit maintains rather than the reader,
        // and it is only useful if it is where they will see it.
        var taskList = TaskList.Create(ownerUserId, ManagedTaskListTitle, [reminderItem], isPinned: true);
        await _taskRepository.AddAsync(taskList, cancellationToken);
        await _managedTaskListRepository.SetTaskListIdAsync(warehouseId, taskList.Id, cancellationToken);
        return taskList.Id;
    }

    /// <summary>
    /// Makes sure an item that is below its minimum has exactly one restock task: reopening the one it
    /// already has if the reader finished it, and creating one otherwise. Returns the (possibly mutated)
    /// item; callers are responsible for persisting it if <see cref="InventoryItem.PendingRestockTaskListId"/>
    /// changed. A no-op, beyond the resolve step, when the item isn't below minimum.
    ///
    /// One task rather than one per save is the whole point: this used to append a second "Restock: X"
    /// as soon as the first was ticked off, so a product that stayed low grew a new entry every time the
    /// warehouse was saved.
    /// </summary>
    public async Task<InventoryItem> EnsureRestockTaskAsync(InventoryItem item, CancellationToken cancellationToken)
    {
        item = await _pendingRestockTaskResolver.ResolveAsync(item, cancellationToken);
        if (!item.IsBelowMinimum)
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

        if (item.PendingRestockTaskItemId is { } trackedTaskItemId)
        {
            var tracked = taskList.Items.First(candidate => candidate.Id == trackedTaskItemId);
            if (!tracked.IsCompleted)
            {
                return item;
            }

            // Still low after being restocked once, so the same entry comes back rather than a new one
            // appearing beside the finished one.
            tracked.Reopen();
            await _taskRepository.UpdateAsync(taskList, cancellationToken);
            return item;
        }

        if (taskList.IsPrivate)
        {
            // The list's items are sealed in the owner's browser, so appending here would either be
            // invisible or - since a private list keeps no readable items - quietly dropped. Better to
            // say so than to leave the warehouse looking like it raised a restock task it didn't.
            throw new InvalidRequestException(
                $"The restock list for this warehouse is private, so Orbit can't add \"{item.Name}\" to it. Turn privacy off for that list first.");
        }

        var restockItem = TaskItem.Create($"Restock: {item.Name}", dueDateUtc: null, isCompleted: false);
        taskList.Update(taskList.Title, [.. taskList.Items, restockItem], taskList.IsGroup, taskList.IsPrivate, taskList.EncryptedContent);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);

        item.SetPendingRestockTask(taskListId, restockItem.Id);
        return item;
    }
}
