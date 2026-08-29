using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Core.Tasks.StockCheck;

namespace Orbit.Core.Tasks.GetTaskListStockCheck;

/// <summary>
/// Null when there is nothing to answer - no such list, or no warehouse chosen for it. Told apart from
/// an empty check, which means the question was asked and the work costs nothing.
/// </summary>
public sealed class GetTaskListStockCheckQueryHandler : IRequestHandler<GetTaskListStockCheckQuery, TaskListStockCheck?>
{
    private readonly ITaskRepository _taskRepository;
    private readonly IInventoryRepository _inventoryRepository;

    public GetTaskListStockCheckQueryHandler(ITaskRepository taskRepository, IInventoryRepository inventoryRepository)
    {
        _taskRepository = taskRepository;
        _inventoryRepository = inventoryRepository;
    }

    public async Task<TaskListStockCheck?> HandleAsync(GetTaskListStockCheckQuery request, CancellationToken cancellationToken)
    {
        var taskList = await _taskRepository.GetByIdAsync(request.UserId, request.TaskListId, cancellationToken);
        if (taskList?.LinkedWarehouseId is not { } warehouseId)
        {
            return null;
        }

        // The whole tree, because a group list's work is on the lists below it rather than on itself.
        var reachable = await _taskRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        var work = LinkedTaskListTree.WorkIn(taskList, reachable);
        var stock = await _inventoryRepository.GetAllAsync(warehouseId, cancellationToken);

        return StockRequirementCounter.Count(work, stock, DateTimeOffset.UtcNow);
    }
}
