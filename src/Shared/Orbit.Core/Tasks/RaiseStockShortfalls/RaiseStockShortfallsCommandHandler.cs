using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Core.Tasks.GetTaskListStockCheck;

namespace Orbit.Core.Tasks.RaiseStockShortfalls;

public sealed class RaiseStockShortfallsCommandHandler : IRequestHandler<RaiseStockShortfallsCommand, int>
{
    private readonly IDispatcher _dispatcher;
    private readonly ITaskRepository _taskRepository;
    private readonly InventoryTaskListCoordinator _inventoryTaskListCoordinator;

    public RaiseStockShortfallsCommandHandler(
        IDispatcher dispatcher, ITaskRepository taskRepository, InventoryTaskListCoordinator inventoryTaskListCoordinator)
    {
        _dispatcher = dispatcher;
        _taskRepository = taskRepository;
        _inventoryTaskListCoordinator = inventoryTaskListCoordinator;
    }

    /// <summary>
    /// Runs the check again rather than trusting a shortfall the caller worked out: what is on the shelf
    /// may have changed since the screen last drew it, and the errand raised here should match the
    /// warehouse as it is now.
    /// </summary>
    public async Task<int> HandleAsync(RaiseStockShortfallsCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskRepository.GetByIdAsync(request.UserId, request.TaskListId, cancellationToken);
        if (taskList?.LinkedWarehouseId is not { } warehouseId)
        {
            return 0;
        }

        var check = await _dispatcher.SendAsync(
            new GetTaskListStockCheckQuery(request.UserId, request.TaskListId), cancellationToken);
        if (check is null)
        {
            return 0;
        }

        // How many are short, not how many the work needs: the shelf already holds the rest, and an
        // errand for eight when six are on the shelf is an errand nobody can read.
        return await _inventoryTaskListCoordinator.EnsureShortfallTasksAsync(
            warehouseId,
            [.. check.Shortfalls.Select(shortfall => new RestockNeed(shortfall.Name, shortfall.Missing))],
            cancellationToken);
    }
}
