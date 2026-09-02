using Orbit.Core.Tasks;

namespace Orbit.Core.Inventory;

/// <summary>What a refresh did, so the screen can say it rather than leave somebody guessing.</summary>
public sealed record RestockRefreshOutcome(int Added, int Removed)
{
    public static readonly RestockRefreshOutcome Nothing = new(0, 0);

    public bool ChangedAnything => Added > 0 || Removed > 0;
}

/// <summary>
/// Rebuilds a warehouse's restock list so it asks for exactly what it should be asking for right now -
/// adding an errand for anything wanted that has none, and taking away an errand for anything no longer
/// wanted.
///
/// Which products are wanted is the warehouse's own choice (see <see cref="RestockListSettings"/>): by
/// default anything below its minimum, or - when the list is set to follow the plan instead - only what
/// some dated task is waiting on. That choice is why this exists at all. The per-save rule in
/// <see cref="InventoryTaskListCoordinator"/> only ever reacts to an item being edited; changing the
/// question the whole list answers cannot be done one item at a time, and neither can noticing that a
/// task somewhere else gained a due date.
/// </summary>
public sealed class RestockListRefresh
{
    private readonly IInventoryManagedTaskListRepository _managedTaskListRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly InventoryTaskListCoordinator _coordinator;

    public RestockListRefresh(
        IInventoryManagedTaskListRepository managedTaskListRepository, IInventoryRepository inventoryRepository,
        IWarehouseRepository warehouseRepository, ITaskRepository taskRepository,
        InventoryTaskListCoordinator coordinator)
    {
        _managedTaskListRepository = managedTaskListRepository;
        _inventoryRepository = inventoryRepository;
        _warehouseRepository = warehouseRepository;
        _taskRepository = taskRepository;
        _coordinator = coordinator;
    }

    public async Task<RestockRefreshOutcome> RefreshAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        if (await _warehouseRepository.GetOwnerUserIdAsync(warehouseId, cancellationToken) is not { } ownerUserId)
        {
            return RestockRefreshOutcome.Nothing;
        }

        if (await _coordinator.EnsureManagedTaskListAsync(warehouseId, cancellationToken) is not { } taskListId)
        {
            return RestockRefreshOutcome.Nothing;
        }

        var taskList = await _taskRepository.GetByIdAsync(ownerUserId, taskListId, cancellationToken);
        if (taskList is null)
        {
            return RestockRefreshOutcome.Nothing;
        }

        var settings = await _managedTaskListRepository.GetSettingsAsync(warehouseId, cancellationToken);
        var shelf = await _inventoryRepository.GetAllAsync(warehouseId, cancellationToken);
        var wanted = await WantedProductIdsAsync(settings, ownerUserId, taskListId, shelf, cancellationToken);

        var kept = new List<TaskItem>();
        var removed = 0;
        foreach (var entry in taskList.Items)
        {
            if (entry.Kind != TaskItemKind.Inventory || entry.LinkedInventoryItemId is not { } inventoryItemId)
            {
                // The standing reminder, and anything somebody put here by hand. Not this method's to
                // decide about: it maintains the errands Orbit raises, not the list's whole contents.
                kept.Add(entry);
                continue;
            }

            if (wanted.Remove(inventoryItemId))
            {
                kept.Add(entry);
                continue;
            }

            removed += 1;
            await ClearPointerToAsync(shelf, inventoryItemId, entry.Id, cancellationToken);
        }

        var added = await NewErrandsForAsync(wanted, shelf, taskListId, cancellationToken);

        UpdateTheStandingReminderTime(kept, settings);
        taskList.Update(
            taskList.Title, [.. kept, .. added], taskList.IsGroup, taskList.IsPrivate, taskList.EncryptedContent,
            taskList.Priority);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);

        return new RestockRefreshOutcome(added.Count, removed);
    }

    /// <summary>
    /// Which products the list should be asking for. The set is mutable on purpose: the caller strikes
    /// off each one it finds an errand for already, so what is left is exactly what needs a new one.
    /// </summary>
    private async Task<HashSet<Guid>> WantedProductIdsAsync(
        RestockListSettings settings, Guid ownerUserId, Guid managedTaskListId, IReadOnlyList<InventoryItem> shelf,
        CancellationToken cancellationToken)
    {
        if (!settings.OnlyLinkedWithDueDate)
        {
            return [.. shelf.Where(item => item.BelongsOnTheRestockList).Select(item => item.Id)];
        }

        var onThisShelf = shelf.Select(item => item.Id).ToHashSet();
        var wanted = new HashSet<Guid>();
        foreach (var other in await _taskRepository.GetAllAsync(ownerUserId, updatedSinceUtc: null, cancellationToken))
        {
            if (other.Id == managedTaskListId)
            {
                // The restock list's own errands carry no due date and would answer nothing; counting
                // them would also make the list keep every errand it already has for ever.
                continue;
            }

            foreach (var entry in other.Items)
            {
                var isDatedProductErrand =
                    entry.Kind == TaskItemKind.Inventory
                    && !entry.IsCompleted
                    && entry.DueDateUtc is not null
                    && entry.LinkedInventoryItemId is { } inventoryItemId
                    && onThisShelf.Contains(inventoryItemId);

                if (isDatedProductErrand)
                {
                    wanted.Add(entry.LinkedInventoryItemId!.Value);
                }
            }
        }

        return wanted;
    }

    private async Task<List<TaskItem>> NewErrandsForAsync(
        HashSet<Guid> wanted, IReadOnlyList<InventoryItem> shelf, Guid taskListId, CancellationToken cancellationToken)
    {
        var added = new List<TaskItem>(wanted.Count);
        foreach (var inventoryItemId in wanted)
        {
            if (shelf.FirstOrDefault(item => item.Id == inventoryItemId) is not { } product)
            {
                continue;
            }

            var errand = TaskItem.Create(
                RestockTaskNaming.EntryFor(product.Name, product.MinimumQuantity, product.Unit),
                dueDateUtc: null, isCompleted: false,
                subject: new TaskItemSubject(TaskItemKind.Inventory, linkedInventoryItemId: product.Id));
            added.Add(errand);

            product.SetPendingRestockTask(taskListId, errand.Id);
            await _inventoryRepository.UpdateAsync(product, cancellationToken);
        }

        return added;
    }

    /// <summary>
    /// Stops a product pointing at an errand that is about to stop existing - the same tidy-up settling a
    /// finished errand does, and for the same reason: a dangling pointer is looked up and not found the
    /// next time that product goes low.
    /// </summary>
    private async Task ClearPointerToAsync(
        IReadOnlyList<InventoryItem> shelf, Guid inventoryItemId, Guid taskItemId, CancellationToken cancellationToken)
    {
        if (shelf.FirstOrDefault(item => item.Id == inventoryItemId) is not { } product
            || product.PendingRestockTaskItemId != taskItemId)
        {
            return;
        }

        product.ClearPendingRestockTask();
        await _inventoryRepository.UpdateAsync(product, cancellationToken);
    }

    /// <summary>
    /// Moves the standing reminder to the hour the warehouse asked for. Done here rather than only when
    /// the list is created, because the setting is changed long after that and a reminder that kept the
    /// old time would make the field look like it did nothing.
    /// </summary>
    private static void UpdateTheStandingReminderTime(List<TaskItem> items, RestockListSettings settings)
    {
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.Description != RestockTaskNaming.UpdateStockReminderDescription || !item.RemindDaily)
            {
                continue;
            }

            items[index] = TaskItem.FromPersistence(
                item.Id, item.Description, item.DueDateUtc, item.IsCompleted, item.LinkedTaskListIds,
                item.Reminders with { DailyTimeOfDay = settings.RefreshTimeOfDay },
                item.Subject);
        }
    }
}
