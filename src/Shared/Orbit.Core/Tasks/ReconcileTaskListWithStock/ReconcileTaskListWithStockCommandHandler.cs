using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Core.Tasks.StockCheck;

namespace Orbit.Core.Tasks.ReconcileTaskListWithStock;

/// <summary>
/// The other half of pricing a list against a warehouse, run in both directions.
///
/// What the shelf covers is work already done, so it is crossed off rather than left for somebody to
/// tick by hand while reading the same numbers off the panel above it. Counted per product, not per
/// entry: a shelf holding three of something crosses off three of the five lines asking for it, oldest
/// first, and leaves the other two. Entries already crossed off count towards what the shelf is
/// covering - three on the shelf and one already done means two more, not three, or the same stock
/// would be spent twice.
///
/// A product the shelf holds and no list mentions is the same disagreement seen from the other side, so
/// it is written onto the list being reconciled. One line per product rather than one per unit: the
/// counting rule reads repetition as quantity, but a shelf holding fifty screws is not fifty errands,
/// and a list nobody could read is not a list.
/// </summary>
public sealed class ReconcileTaskListWithStockCommandHandler : IRequestHandler<ReconcileTaskListWithStockCommand, StockReconciliation>
{
    private readonly ITaskRepository _taskRepository;
    private readonly IInventoryRepository _inventoryRepository;

    public ReconcileTaskListWithStockCommandHandler(
        ITaskRepository taskRepository, IInventoryRepository inventoryRepository)
    {
        _taskRepository = taskRepository;
        _inventoryRepository = inventoryRepository;
    }

    public async Task<StockReconciliation> HandleAsync(ReconcileTaskListWithStockCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskRepository.GetByIdAsync(request.UserId, request.TaskListId, cancellationToken);
        if (taskList?.LinkedWarehouseId is not { } warehouseId)
        {
            return StockReconciliation.Nothing;
        }

        var reachable = await _taskRepository.GetAllAsync(request.UserId, cancellationToken);
        var tree = LinkedTaskListTree.Flatten(taskList, reachable);
        var stock = await _inventoryRepository.GetAllAsync(warehouseId, cancellationToken);

        var changedLists = new HashSet<TaskList>();
        var added = AddWhatOnlyTheShelfKnowsAbout(taskList, tree, stock);
        if (added > 0)
        {
            changedLists.Add(taskList);
        }

        var covered = CountBy(stock);
        var nowUtc = DateTimeOffset.UtcNow;
        var crossedOff = 0;
        foreach (var list in tree)
        {
            var crossedOffHere = CrossOffWhatIsCovered(list, covered, nowUtc);
            if (crossedOffHere == 0)
            {
                continue;
            }

            crossedOff += crossedOffHere;
            changedLists.Add(list);
        }

        if (changedLists.Count == 0)
        {
            return StockReconciliation.Nothing;
        }

        await _taskRepository.UpdateManyAsync([.. changedLists], cancellationToken);
        return new StockReconciliation(crossedOff, added);
    }

    /// <summary>
    /// Writes onto <paramref name="taskList"/> every product the shelf holds that nothing in the tree
    /// asks for. Answers how many were written. A private list is left alone: its items are sealed in
    /// its owner's browser, so anything appended here would be dropped rather than shown.
    /// </summary>
    private static int AddWhatOnlyTheShelfKnowsAbout(
        TaskList taskList, IReadOnlyList<TaskList> tree, IReadOnlyList<InventoryItem> stock)
    {
        if (taskList.IsPrivate)
        {
            return 0;
        }

        var alreadyAskedFor = tree
            .SelectMany(list => list.Items)
            .Where(item => item.LinkedTaskListId is null)
            .Select(item => Normalize(item.Description))
            .ToHashSet();

        var newItems = new List<TaskItem>();
        foreach (var shelfItem in stock)
        {
            var key = Normalize(shelfItem.Name);
            if (key.Length == 0 || !alreadyAskedFor.Add(key))
            {
                continue;
            }

            newItems.Add(TaskItem.Create(shelfItem.Name.Trim(), dueDateUtc: null, isCompleted: false));
        }

        if (newItems.Count == 0)
        {
            return 0;
        }

        taskList.Update(
            taskList.Title, [.. taskList.Items, .. newItems], taskList.IsGroup, taskList.IsPrivate,
            taskList.EncryptedContent, taskList.Priority);
        return newItems.Count;
    }

    /// <summary>
    /// Crosses off entries in one list while the shelf still has that product to spend, spending it as
    /// it goes so the next list down does not spend the same units again. Answers how many it crossed.
    /// </summary>
    private static int CrossOffWhatIsCovered(TaskList taskList, Dictionary<string, decimal> covered, DateTimeOffset nowUtc)
    {
        var crossedOff = 0;
        foreach (var item in taskList.Items)
        {
            if (item.LinkedTaskListId is not null || IsNotDueYet(item, nowUtc))
            {
                continue;
            }

            var key = Normalize(item.Description);
            if (key.Length == 0 || covered.GetValueOrDefault(key) < 1)
            {
                continue;
            }

            // Spent whether or not this entry needed crossing off: an entry already done is what the
            // reader fetched it for, and counting it again would cross off a second line for free.
            covered[key] -= 1;
            if (item.IsCompleted)
            {
                continue;
            }

            item.Complete();
            crossedOff += 1;
        }

        return crossedOff;
    }

    private static Dictionary<string, decimal> CountBy(IEnumerable<InventoryItem> stock)
        => stock
            .GroupBy(item => Normalize(item.Name))
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

    /// <summary>Work that has not come round yet is not work the shelf can finish - see StockRequirementCounter.</summary>
    private static bool IsNotDueYet(TaskItem item, DateTimeOffset nowUtc)
        => item.DueDateUtc is { } dueDate && dueDate > nowUtc;

    private static string Normalize(string name) => name.Trim().ToLowerInvariant();
}
