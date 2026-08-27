using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.CreateWarehouse;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks.StockCheck;

namespace Orbit.Core.Tasks.GenerateWarehouseFromTaskList;

/// <summary>
/// Turns a list of work into the shelf that work needs: one entry per distinct thing it calls for, each
/// starting at nothing, and the list pointed at the result so the stock check can be run straight away.
///
/// Everything the tree names is included, including lines dated in the future - the shelf is for what
/// the job will need, while the check counts only what is due. Quantities start at zero rather than at
/// what the work needs: a shelf that began full would report the job as doable before anybody had
/// fetched anything.
/// </summary>
public sealed class GenerateWarehouseFromTaskListCommandHandler : IRequestHandler<GenerateWarehouseFromTaskListCommand, Guid?>
{
    /// <summary>What a generated entry is filed under until somebody says otherwise.</summary>
    private const string GeneratedProductType = "Part";
    private const string GeneratedCategory = "From a task list";

    private readonly IDispatcher _dispatcher;
    private readonly ITaskRepository _taskRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly InventoryTaskListCoordinator _taskListCoordinator;

    public GenerateWarehouseFromTaskListCommandHandler(
        IDispatcher dispatcher, ITaskRepository taskRepository, IInventoryRepository inventoryRepository,
        InventoryTaskListCoordinator taskListCoordinator)
    {
        _dispatcher = dispatcher;
        _taskRepository = taskRepository;
        _inventoryRepository = inventoryRepository;
        _taskListCoordinator = taskListCoordinator;
    }

    public async Task<Guid?> HandleAsync(GenerateWarehouseFromTaskListCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskRepository.GetByIdAsync(request.UserId, request.TaskListId, cancellationToken);
        if (taskList is null || taskList.UserId != request.UserId)
        {
            return null;
        }

        var reachable = await _taskRepository.GetAllAsync(request.UserId, cancellationToken);
        var names = LinkedTaskListTree.WorkIn(taskList, reachable)
            .Select(item => item.Description.Trim())
            .Where(description => description.Length > 0)
            .DistinctBy(description => description.ToLowerInvariant())
            .ToList();

        var warehouseId = await _dispatcher.SendAsync(
            new CreateWarehouseCommand(request.UserId, taskList.Title), cancellationToken);

        // The rows go in one at a time rather than through UpdateWarehouseCommand: that command writes
        // the warehouse row as well, and a warehouse created and updated inside one request leaves the
        // same key tracked twice.
        foreach (var name in names)
        {
            await _inventoryRepository.AddAsync(
                InventoryItem.Create(
                    warehouseId, name, GeneratedProductType, GeneratedCategory, quantity: 0,
                    minimumQuantity: null, expiryDate: null, NotificationChannel.None),
                cancellationToken);
        }

        // The standing "keep your stock updated" reminder exists from a warehouse's first item, the same
        // as when items are added through the warehouse editor.
        if (names.Count > 0)
        {
            await _taskListCoordinator.EnsureManagedTaskListAsync(warehouseId, cancellationToken);
        }

        taskList.LinkToWarehouse(warehouseId);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);
        return warehouseId;
    }
}
