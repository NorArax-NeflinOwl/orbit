using Orbit.Core.Notifications;
using Orbit.Core.Tasks;

namespace Orbit.Core.Inventory;

/// <summary>
/// Creates and maintains the single, system-managed TaskList ("Restock supplies") that Inventory uses
/// for both the standing "keep your stock updated" reminder and every per-product restock task - see
/// IInventoryManagedTaskListRepository for why this is tracked outside the Tasks schema entirely.
/// </summary>
public sealed class InventoryTaskListCoordinator
{
    /// <summary>Title of the system-managed task list this coordinator creates/reuses per user.</summary>
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
    private readonly PendingRestockTaskResolver _pendingRestockTaskResolver;

    public InventoryTaskListCoordinator(
        ITaskRepository taskRepository, IInventoryManagedTaskListRepository managedTaskListRepository,
        PendingRestockTaskResolver pendingRestockTaskResolver)
    {
        _taskRepository = taskRepository;
        _managedTaskListRepository = managedTaskListRepository;
        _pendingRestockTaskResolver = pendingRestockTaskResolver;
    }

    /// <summary>
    /// Ensures userId has a managed TaskList (creating it, with the standing reminder item, the first
    /// time it's needed) and returns its id. Also re-creates it if the previously tracked list was
    /// deleted out from under this tracking - the missing-list case is treated the same as never having
    /// had one, not an error.
    /// </summary>
    public async Task<Guid> EnsureManagedTaskListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var trackedTaskListId = await _managedTaskListRepository.GetTaskListIdAsync(userId, cancellationToken);
        if (trackedTaskListId is { } existingId)
        {
            var existingTaskList = await _taskRepository.GetByIdAsync(userId, existingId, cancellationToken);
            if (existingTaskList is not null)
            {
                return existingId;
            }
        }

        var reminderItem = TaskItem.Create(
            UpdateStockReminderDescription, dueDateUtc: null, isCompleted: false,
            remindDaily: true, dailyReminderNotificationChannel: NotificationChannel.Push);
        var taskList = TaskList.Create(userId, ManagedTaskListTitle, [reminderItem]);
        await _taskRepository.AddAsync(taskList, cancellationToken);
        await _managedTaskListRepository.SetTaskListIdAsync(userId, taskList.Id, cancellationToken);
        return taskList.Id;
    }

    /// <summary>
    /// Resolves item's pending-task state, then - if it's below minimum and nothing is already open -
    /// appends a fresh restock TaskItem to the managed list. Returns the (possibly mutated) item;
    /// callers are responsible for persisting it if <see cref="InventoryItem.PendingRestockTaskListId"/>
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

        var taskListId = await EnsureManagedTaskListAsync(item.UserId, cancellationToken);
        var taskList = await _taskRepository.GetByIdAsync(item.UserId, taskListId, cancellationToken)
            ?? throw new InvalidOperationException($"Managed task list {taskListId} for user {item.UserId} disappeared between ensuring it and using it.");

        var restockItem = TaskItem.Create($"Restock: {item.Name}", dueDateUtc: null, isCompleted: false);
        taskList.Update(taskList.Title, [.. taskList.Items, restockItem]);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);

        item.SetPendingRestockTask(taskListId, restockItem.Id);
        return item;
    }
}
