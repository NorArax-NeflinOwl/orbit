using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Core.Tasks.StockCheck;

namespace Orbit.Core.Tasks.GetTaskListStockCheck;

/// <summary>
/// Null when there is nothing to answer - no such list, or no inventory chosen for it. Told apart from
/// an empty check, which means the question was asked and the work costs nothing.
/// </summary>
public sealed class GetTaskListStockCheckQueryHandler : IRequestHandler<GetTaskListStockCheckQuery, TaskListStockCheck?>
{
    private readonly ITaskRepository _taskRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;

    public GetTaskListStockCheckQueryHandler(ITaskRepository taskRepository, IInventoryItemRepository inventoryItemRepository)
    {
        _taskRepository = taskRepository;
        _inventoryItemRepository = inventoryItemRepository;
    }

    public async Task<TaskListStockCheck?> HandleAsync(GetTaskListStockCheckQuery request, CancellationToken cancellationToken)
    {
        var taskList = await _taskRepository.GetByIdAsync(request.UserId, request.TaskListId, cancellationToken);
        if (taskList?.LinkedInventoryId is not { } inventoryId)
        {
            return null;
        }

        // The whole tree, because a group list's work is on the lists below it rather than on itself.
        var reachable = await _taskRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        var work = LinkedTaskListTree.WorkIn(taskList, reachable);
        var stock = await _inventoryItemRepository.GetAllAsync(inventoryId, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        return StockRequirementCounter.Count(
            work, stock, now, AskedForByTheOtherLists(taskList, reachable, inventoryId, now));
    }

    /// <summary>
    /// What every other list measured against this inventory asks for, by name. A shelf serves all of
    /// them, so the answer to "is there enough" is about the whole demand on it: without this, two lists
    /// each wanting the last bag of flour would both be told the bag is theirs.
    ///
    /// Each list is counted through its own tree, the same way this one is, and a list appearing in
    /// another's tree is not counted twice - what a group list stands for is already in it.
    /// </summary>
    private static IReadOnlyDictionary<string, decimal> AskedForByTheOtherLists(
        TaskList taskList, IReadOnlyList<TaskList> reachable, Guid inventoryId, DateTimeOffset nowUtc)
    {
        var alreadyCounted = LinkedTaskListTree.Flatten(taskList, reachable).Select(list => list.Id).ToHashSet();
        var elsewhere = new Dictionary<string, decimal>();

        foreach (var other in reachable.Where(candidate =>
            candidate.LinkedInventoryId == inventoryId && !alreadyCounted.Contains(candidate.Id)))
        {
            foreach (var (name, quantity) in StockRequirementCounter.DemandOf(
                LinkedTaskListTree.WorkIn(other, reachable), nowUtc))
            {
                elsewhere[name] = elsewhere.GetValueOrDefault(name) + quantity;
            }

            // A list already counted through its own tree must not be counted again through the next
            // list's - two group lists can stand for the same member.
            foreach (var counted in LinkedTaskListTree.Flatten(other, reachable))
            {
                alreadyCounted.Add(counted.Id);
            }
        }

        return elsewhere;
    }
}
