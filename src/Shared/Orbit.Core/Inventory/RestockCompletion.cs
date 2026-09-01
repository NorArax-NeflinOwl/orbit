using Orbit.Core.Tasks;

namespace Orbit.Core.Inventory;

/// <summary>
/// What was done, so a caller can say so rather than guess. <paramref name="ToppedUp"/> counts shelf
/// items actually raised - an errand for something already back above its minimum is finished without
/// moving anything, and still counts as <paramref name="Removed"/>.
/// </summary>
public sealed record RestockOutcome(int ToppedUp, int Removed)
{
    public static readonly RestockOutcome Nothing = new(0, 0);

    public bool ChangedAnything => ToppedUp > 0 || Removed > 0;
}

/// <summary>
/// What finishing a restock errand means to the shelf it came from, and to the list it sits on.
/// Crossing off "Restock: Flour (5)" says somebody went and got the flour, so two things follow: the
/// shelf is brought up to the level it is meant to hold, and the errand leaves the list - it is no
/// longer something missing, and a permanently crossed-off line is a list that stops being read.
///
/// Only ever raises an amount. Finishing an errand is not a claim about how much is there beyond the
/// minimum, and somebody who stocked more than that should not have it taken away.
/// </summary>
public sealed class RestockCompletion
{
    private readonly IInventoryManagedTaskListRepository _managedTaskListRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ITaskRepository _taskRepository;

    public RestockCompletion(
        IInventoryManagedTaskListRepository managedTaskListRepository, IInventoryRepository inventoryRepository,
        IWarehouseRepository warehouseRepository, ITaskRepository taskRepository)
    {
        _managedTaskListRepository = managedTaskListRepository;
        _inventoryRepository = inventoryRepository;
        _warehouseRepository = warehouseRepository;
        _taskRepository = taskRepository;
    }

    /// <summary>
    /// Settles every finished errand on <paramref name="taskListId"/> against the warehouse behind it:
    /// each one tops its shelf item up to the minimum and then leaves the list. Answers with what moved.
    ///
    /// Safe to call on any list and safe to call repeatedly - it does nothing at all for an ordinary
    /// list, which is the case almost every save takes, and a second run finds nothing left to settle.
    /// That is what lets it run both when the list is saved and when it is opened, which is how a list
    /// that accumulated crossed-off errands before this existed heals itself.
    /// </summary>
    public Task<RestockOutcome> ReconcileAsync(Guid taskListId, CancellationToken cancellationToken)
        => SettleAsync(taskListId, takeFinishedOffTheList: true, cancellationToken);

    /// <summary>
    /// The shelf half on its own: finished errands top their shelf items up, and then stay on the list,
    /// crossed off.
    ///
    /// This is what an ordinary save does. Crossing something off used to take it away in the same
    /// breath, so a row answered a tap by vanishing - and a tap on the wrong row could not be undone by
    /// untapping it, because there was nothing left to untap. The entry now stays until a refresh
    /// clears it, which the checklist asks for a few minutes later: long enough to notice a mistake,
    /// short enough that the list still tidies itself without anybody thinking about it.
    /// </summary>
    public Task<RestockOutcome> TopUpFinishedAsync(Guid taskListId, CancellationToken cancellationToken)
        => SettleAsync(taskListId, takeFinishedOffTheList: false, cancellationToken);

    private async Task<RestockOutcome> SettleAsync(
        Guid taskListId, bool takeFinishedOffTheList, CancellationToken cancellationToken)
    {
        if (await _managedTaskListRepository.GetWarehouseIdAsync(taskListId, cancellationToken) is not { } warehouseId)
        {
            return RestockOutcome.Nothing;
        }

        if (await _warehouseRepository.GetOwnerUserIdAsync(warehouseId, cancellationToken) is not { } ownerUserId)
        {
            return RestockOutcome.Nothing;
        }

        var taskList = await _taskRepository.GetByIdAsync(ownerUserId, taskListId, cancellationToken);
        if (taskList is null)
        {
            return RestockOutcome.Nothing;
        }

        var shelf = await _inventoryRepository.GetAllAsync(warehouseId, cancellationToken);
        var finished = taskList.Items.Where(IsFinishedErrand).ToList();
        if (finished.Count == 0)
        {
            return RestockOutcome.Nothing;
        }

        var toppedUp = 0;
        var settled = new HashSet<Guid>();
        foreach (var errand in finished)
        {
            settled.Add(errand.Id);

            if (FindShelfItem(errand, shelf) is not { } shelfItem)
            {
                // The product is gone from the warehouse. The errand is still finished - there is
                // nothing left to bring back - so it leaves the list rather than staying forever.
                continue;
            }

            if (shelfItem.TopUpToMinimum())
            {
                toppedUp += 1;
            }

            // The entry is about to stop existing, so the item must stop pointing at it - otherwise the
            // next time this product goes low, EnsureRestockTaskAsync looks for an entry that is gone.
            // While it is only being topped up the entry is still there, and the link with it: cutting
            // it early would let a second errand for the same product appear beside the first.
            if (takeFinishedOffTheList && shelfItem.PendingRestockTaskItemId == errand.Id)
            {
                shelfItem.ClearPendingRestockTask();
            }

            await _inventoryRepository.UpdateAsync(shelfItem, cancellationToken);
        }

        if (!takeFinishedOffTheList)
        {
            return new RestockOutcome(toppedUp, Removed: 0);
        }

        taskList.Update(
            taskList.Title, [.. taskList.Items.Where(item => !settled.Contains(item.Id))], taskList.IsGroup,
            taskList.IsPrivate, taskList.EncryptedContent, taskList.Priority);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);

        return new RestockOutcome(toppedUp, settled.Count);
    }

    /// <summary>
    /// Brings every item in the warehouse behind <paramref name="taskListId"/> up to its minimum - the
    /// answer to "yes, I have restocked everything" rather than crossing off one errand at a time.
    /// </summary>
    public async Task<int> TopUpEverythingAsync(Guid taskListId, CancellationToken cancellationToken)
    {
        if (await _managedTaskListRepository.GetWarehouseIdAsync(taskListId, cancellationToken) is not { } warehouseId)
        {
            return 0;
        }

        var toppedUp = 0;
        foreach (var item in await _inventoryRepository.GetAllAsync(warehouseId, cancellationToken))
        {
            if (!item.TopUpToMinimum())
            {
                continue;
            }

            await _inventoryRepository.UpdateAsync(item, cancellationToken);
            toppedUp += 1;
        }

        return toppedUp;
    }

    /// <summary>
    /// A crossed-off restock errand, and nothing else on the list. The standing "Update stock levels"
    /// reminder is deliberately not one: crossing it off is a claim about the whole shelf rather than
    /// about a product, and it comes back daily rather than leaving.
    /// </summary>
    private static bool IsFinishedErrand(TaskItem item)
        => item.IsCompleted
            && (item.Kind == TaskItemKind.Inventory || RestockTaskNaming.IsRestockEntry(item.Description));

    /// <summary>
    /// The shelf item an errand is about. The link is the answer wherever there is one; falling back to
    /// the product name in the description is what settles entries written before the link existed, and
    /// is why a list that has been sitting there with old crossed-off errands settles on first sight.
    /// </summary>
    private static InventoryItem? FindShelfItem(TaskItem errand, IReadOnlyList<InventoryItem> shelf)
    {
        if (errand.LinkedInventoryItemId is { } linkedId)
        {
            return shelf.FirstOrDefault(item => item.Id == linkedId);
        }

        var product = RestockTaskNaming.ProductIn(errand.Description);
        var byName = shelf
            .Where(item => string.Equals(item.Name.Trim(), product, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        // One match or none: two products sharing a name give no answer to "which one", and topping up
        // the wrong shelf is worse than leaving the errand for somebody to settle by hand.
        return byName.Count == 1 ? byName[0] : null;
    }
}
