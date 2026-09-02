using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;

namespace Orbit.Core.Tasks.LinkTaskListToWarehouse;

/// <summary>
/// Only the list's owner may point it at a warehouse, and only at a warehouse they can actually read -
/// otherwise the stock check would report on shelves its reader never had access to.
///
/// Several lists may share one warehouse. That used to be refused, because two lists measured against
/// one shelf each reported a shortfall the other had already accounted for; what answers that is the
/// counting rather than a ban - a shelf is now measured against everything asking for it at once, and
/// each list is told its share (see GetTaskListStockCheckQueryHandler). A list still points at one
/// warehouse: work is done out of one store.
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
