using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;

namespace Orbit.Core.Inventories;

/// <summary>
/// Creates and maintains the single, system-managed TaskList ("Restock supplies") that Inventory uses
/// for both the standing "keep your stock updated" reminder and every per-product restock task - one
/// such list per inventory, so two inventories don't pile their restock tasks into the same list. See
/// IInventoryManagedTaskListRepository for why this is tracked outside the Tasks schema entirely.
///
/// The list itself belongs to the inventory's *owner*, since a TaskList is owned by a user - this looks
/// that owner up itself rather than making every caller pass it alongside the inventory id.
/// </summary>
public sealed class InventoryTaskListCoordinator
{

    /// <summary>
    /// The standing, never-recreated reminder task - see RestockTaskNaming for its wording. RemindDaily
    /// brings it back every day at its own time of day, whether or not the reader ticked it off
    /// yesterday, which is what makes one task enough instead of a new one appearing each morning.
    /// Tasks has no recurrence engine to build a self-recreating task on top of, and this covers the
    /// same intent without one.
    /// </summary>
    public const string UpdateStockReminderDescription = RestockTaskNaming.UpdateStockReminderDescription;

    private readonly ITaskRepository _taskRepository;
    private readonly IInventoryManagedTaskListRepository _managedTaskListRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly PendingRestockTaskResolver _pendingRestockTaskResolver;

    public InventoryTaskListCoordinator(
        ITaskRepository taskRepository, IInventoryManagedTaskListRepository managedTaskListRepository,
        IInventoryRepository inventoryRepository, IInventoryItemRepository inventoryItemRepository,
        PendingRestockTaskResolver pendingRestockTaskResolver)
    {
        _taskRepository = taskRepository;
        _managedTaskListRepository = managedTaskListRepository;
        _inventoryRepository = inventoryRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _pendingRestockTaskResolver = pendingRestockTaskResolver;
    }

    /// <summary>
    /// Ensures inventoryId has a managed TaskList (creating it, with the standing reminder item, the
    /// first time it's needed) and returns its id. Also re-creates it if the previously tracked list was
    /// deleted out from under this tracking - the missing-list case is treated the same as never having
    /// had one, not an error. Returns null when the inventory itself is gone.
    /// </summary>
    public async Task<Guid?> EnsureManagedTaskListAsync(Guid inventoryId, CancellationToken cancellationToken)
    {
        if (await _inventoryRepository.GetOwnerUserIdAsync(inventoryId, cancellationToken) is not { } ownerUserId)
        {
            return null;
        }

        var title = RestockTaskNaming.TitleFor(
            (await _inventoryRepository.GetByIdAsync(ownerUserId, inventoryId, cancellationToken))?.Name ?? string.Empty);

        var trackedTaskListId = await _managedTaskListRepository.GetTaskListIdAsync(inventoryId, cancellationToken);
        if (trackedTaskListId is { } existingId)
        {
            var existingTaskList = await _taskRepository.GetByIdAsync(ownerUserId, existingId, cancellationToken);
            if (existingTaskList is not null)
            {
                await RenameIfTheInventoryWasRenamedAsync(existingTaskList, title, cancellationToken);
                return existingId;
            }
        }

        // The hour the inventory asked for, not a constant: nine in the morning is only the default (see
        // RestockListSettings), and a list created after somebody changed it should come round when they
        // said rather than when Orbit first guessed.
        var settings = await _managedTaskListRepository.GetSettingsAsync(inventoryId, cancellationToken);
        var reminderItem = TaskItem.Create(
            UpdateStockReminderDescription, dueDateUtc: null, isCompleted: false,
            reminders: TaskItemReminders.Default with { Daily = true, DailyTimeOfDay = settings.RefreshTimeOfDay });
        // Pinned from the moment it exists: this is the one list Orbit maintains rather than the reader,
        // and it is only useful if it is where they will see it.
        var taskList = TaskList.Create(ownerUserId, title, [reminderItem], isPinned: true);
        await _taskRepository.AddAsync(taskList, cancellationToken);
        await _managedTaskListRepository.SetTaskListIdAsync(inventoryId, taskList.Id, cancellationToken);
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
    /// inventory was saved.
    /// </summary>
    public async Task<InventoryItem> EnsureRestockTaskAsync(InventoryItem item, CancellationToken cancellationToken)
    {
        item = await _pendingRestockTaskResolver.ResolveAsync(item, cancellationToken);
        if (!item.BelongsOnTheRestockList)
        {
            return item;
        }

        // A list that follows the plan does not answer "what is running out", so a product dropping below
        // its minimum is not by itself a reason to put it there - see RestockListSettings. What that list
        // asks for is worked out across every task at once, which is RestockListRefresh's job.
        var settings = await _managedTaskListRepository.GetSettingsAsync(item.InventoryId, cancellationToken);
        if (settings.OnlyLinkedWithDueDate)
        {
            return item;
        }

        if (await EnsureManagedTaskListAsync(item.InventoryId, cancellationToken) is not { } taskListId)
        {
            return item;
        }

        var ownerUserId = await _inventoryRepository.GetOwnerUserIdAsync(item.InventoryId, cancellationToken)
            ?? throw new InvalidOperationException($"Inventory {item.InventoryId} disappeared between ensuring its task list and using it.");
        var taskList = await _taskRepository.GetByIdAsync(ownerUserId, taskListId, cancellationToken)
            ?? throw new InvalidOperationException($"Managed task list {taskListId} for inventory {item.InventoryId} disappeared between ensuring it and using it.");

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
            // say so than to leave the inventory looking like it raised a restock task it didn't.
            throw new InvalidRequestException(
                $"The restock list for this inventory is private, so Orbit can't add \"{item.Name}\" to it. Turn privacy off for that list first.");
        }

        // Kind and link, not just a sentence: this is what lets the entry be reconciled against the shelf
        // it came from without parsing a product name back out of its own description - see
        // TaskItemKind.Inventory and RestockReconciliation.
        var restockItem = TaskItem.Create(
            RestockTaskNaming.EntryFor(item.Name, item.MinimumQuantity, item.Unit), dueDateUtc: null, isCompleted: false,
            subject: new TaskItemSubject(TaskItemKind.Inventory, linkedInventoryItemId: item.Id));
        taskList.Update(
            taskList.Title, [.. taskList.Items, restockItem], taskList.IsGroup, taskList.IsPrivate,
            taskList.EncryptedContent, taskList.Priority);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);

        item.SetPendingRestockTask(taskListId, restockItem.Id);
        return item;
    }

    /// <summary>
    /// Puts what a task list's work is short of onto the inventory's standing restock list, so the
    /// missing things arrive with the daily reminder rather than only on the screen that worked them out.
    ///
    /// Names already sitting there unticked are left alone: the same shortfall recalculated tomorrow is
    /// the same errand, not a second one. Returns how many were actually added.
    /// </summary>
    public async Task<int> EnsureShortfallTasksAsync(
        Guid inventoryId, IReadOnlyCollection<RestockNeed> needs, CancellationToken cancellationToken)
    {
        if (needs.Count == 0 || await EnsureManagedTaskListAsync(inventoryId, cancellationToken) is not { } taskListId)
        {
            return 0;
        }

        var ownerUserId = await _inventoryRepository.GetOwnerUserIdAsync(inventoryId, cancellationToken)
            ?? throw new InvalidOperationException($"Inventory {inventoryId} disappeared between ensuring its task list and using it.");
        var taskList = await _taskRepository.GetByIdAsync(ownerUserId, taskListId, cancellationToken)
            ?? throw new InvalidOperationException($"Managed task list {taskListId} for inventory {inventoryId} disappeared between ensuring it and using it.");

        if (taskList.IsPrivate)
        {
            // Same reason as EnsureRestockTaskAsync above: a private list keeps no readable items, so
            // appending here would be quietly dropped.
            throw new InvalidRequestException(
                "The restock list for this inventory is private, so Orbit can't add what's missing to it. Turn privacy off for that list first.");
        }

        // Matched on the product rather than the whole line: an errand for five of something and one
        // for eight of it are the same errand, and a changed minimum must not put a second copy on the
        // list beside the first.
        var alreadyWaiting = taskList.Items
            .Where(item => !item.IsCompleted)
            .Select(item => RestockTaskNaming.ProductIn(item.Description))
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);

        // A shortfall is named rather than pointed at - it is counted off a checklist, which knows product
        // names and not shelf ids. Where the inventory does hold a product by that name, the entry is
        // linked to it anyway, so it reconciles like any other inventory errand; where it does not, the
        // entry stays an ordinary line and is simply crossed off by hand.
        var shelf = (await _inventoryItemRepository.GetAllAsync(inventoryId, cancellationToken))
            .GroupBy(shelfItem => shelfItem.Name.Trim(), StringComparer.CurrentCultureIgnoreCase)
            // One match or none: two products sharing a name give no answer to "which one", and guessing
            // would top up the wrong shelf.
            .Where(byName => byName.Count() == 1)
            .ToDictionary(byName => byName.Key, byName => byName.Single().Id, StringComparer.CurrentCultureIgnoreCase);

        var added = needs
            .Where(need => alreadyWaiting.Add(need.ProductName.Trim()))
            .Select(need => NewShortfallEntry(need, shelf))
            .ToList();
        if (added.Count == 0)
        {
            return 0;
        }

        taskList.Update(
            taskList.Title, [.. taskList.Items, .. added], taskList.IsGroup, taskList.IsPrivate,
            taskList.EncryptedContent, taskList.Priority);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);
        return added.Count;
    }

    /// <summary>
    /// Keeps the list's title in step with its inventory's name. Only a title Orbit itself wrote is
    /// touched: a reader who renamed the list meant to.
    /// </summary>
    private async Task RenameIfTheInventoryWasRenamedAsync(
        TaskList taskList, string title, CancellationToken cancellationToken)
    {
        if (taskList.Title == title || !RestockTaskNaming.IsManagedTitle(taskList.Title))
        {
            return;
        }

        taskList.Update(
            title, taskList.Items, taskList.IsGroup, taskList.IsPrivate, taskList.EncryptedContent, taskList.Priority);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);
    }

    /// <summary>
    /// One shortfall as an entry, linked to the shelf item it names when the inventory holds exactly one
    /// by that name.
    /// </summary>
    private static TaskItem NewShortfallEntry(RestockNeed need, IReadOnlyDictionary<string, Guid> shelf)
    {
        var description = RestockTaskNaming.EntryFor(need.ProductName, need.Quantity, unit: null);
        return shelf.TryGetValue(need.ProductName.Trim(), out var inventoryItemId)
            ? TaskItem.Create(
                description, dueDateUtc: null, isCompleted: false,
                subject: new TaskItemSubject(TaskItemKind.Inventory, linkedInventoryItemId: inventoryItemId))
            : TaskItem.Create(description, dueDateUtc: null, isCompleted: false);
    }
}

/// <summary>
/// One thing to bring back, and how many of it - see RestockTaskNaming.EntryFor. No unit, deliberately:
/// this is counted off a checklist, where repetition is the quantity (see StockRequirementCounter), so
/// the number is a count of lines rather than an amount of anything measurable.
/// </summary>
public sealed record RestockNeed(string ProductName, decimal? Quantity);
