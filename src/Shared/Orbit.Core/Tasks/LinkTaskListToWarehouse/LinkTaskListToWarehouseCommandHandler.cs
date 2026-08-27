using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;

namespace Orbit.Core.Tasks.LinkTaskListToWarehouse;

/// <summary>
/// Only the list's owner may point it at a warehouse, and only at a warehouse they can actually read -
/// otherwise the stock check would report on shelves its reader never had access to.
/// </summary>
public sealed class LinkTaskListToWarehouseCommandHandler : IRequestHandler<LinkTaskListToWarehouseCommand, bool>
{
    private readonly ITaskRepository _taskRepository;
    private readonly IWarehouseRepository _warehouseRepository;

    public LinkTaskListToWarehouseCommandHandler(ITaskRepository taskRepository, IWarehouseRepository warehouseRepository)
    {
        _taskRepository = taskRepository;
        _warehouseRepository = warehouseRepository;
    }

    public async Task<bool> HandleAsync(LinkTaskListToWarehouseCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskRepository.GetByIdAsync(request.UserId, request.TaskListId, cancellationToken);
        if (taskList is null || taskList.UserId != request.UserId)
        {
            return false;
        }

        if (request.WarehouseId is { } warehouseId
            && await _warehouseRepository.GetByIdAsync(request.UserId, warehouseId, cancellationToken) is null)
        {
            return false;
        }

        taskList.LinkToWarehouse(request.WarehouseId);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);
        return true;
    }
}
