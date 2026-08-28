using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.CreateWarehouse;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks.StockCheck;

namespace Orbit.Core.Tasks.GenerateWarehouseFromTaskList;

/// <summary>
/// Turns a list of work into the shelf that work needs: one entry per distinct thing it calls for, each
/// carrying how many the job needs as its minimum, and the list pointed at the result so the stock check
/// can be run straight away.
///
/// The minimum is counted the same way the check counts - repetition is quantity, so pasta named in
/// three recipes has a minimum of three - which is what makes a generated shelf a shopping list rather
/// than a list of headings. What starts on the shelf is what the work has already ticked off: a line
/// somebody has crossed out is a thing they have, so three recipes with one done reads as one of three
/// rather than none.
///
/// Everything the tree names is included, including lines dated in the future - the shelf holds what the
/// whole job will need, while the check counts only what is due.
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
        var needed = StockRequirementCounter
            .CountRegardlessOfDueDate(LinkedTaskListTree.WorkIn(taskList, reachable))
            .Requirements;

        var warehouseId = await _dispatcher.SendAsync(
            new CreateWarehouseCommand(request.UserId, taskList.Title), cancellationToken);

        // The rows go in one at a time rather than through UpdateWarehouseCommand: that command writes
        // the warehouse row as well, and a warehouse created and updated inside one request leaves the
        // same key tracked twice.
        // In the order the work asks for things rather than alphabetically: a shelf built from a
        // shopping list reads best in the order the list reads - see InventoryItem.Position.
        foreach (var (requirement, position) in needed.Select((requirement, position) => (requirement, position)))
        {
            await _inventoryRepository.AddAsync(
                InventoryItem.Create(
                    warehouseId, requirement.Name, GeneratedProductType, GeneratedCategory, requirement.Done,
                    minimumQuantity: requirement.Required, expiryDate: null, NotificationChannel.None, position),
                cancellationToken);
        }

        // The standing "keep your stock updated" reminder exists from a warehouse's first item, the same
        // as when items are added through the warehouse editor.
        if (needed.Count > 0)
        {
            await _taskListCoordinator.EnsureManagedTaskListAsync(warehouseId, cancellationToken);
        }

        taskList.LinkToWarehouse(warehouseId);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);
        return warehouseId;
    }
}
