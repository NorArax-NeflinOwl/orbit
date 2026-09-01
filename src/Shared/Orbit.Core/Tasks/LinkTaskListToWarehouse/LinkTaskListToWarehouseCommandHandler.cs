using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;

namespace Orbit.Core.Tasks.LinkTaskListToWarehouse;

/// <summary>
/// Only the list's owner may point it at a warehouse, and only at a warehouse they can actually read -
/// otherwise the stock check would report on shelves its reader never had access to.
///
/// And only one list per warehouse. A shelf measured against two lists has two answers to "is there
/// enough", and each list's stock check would report a shortfall the other list had already accounted
/// for - so the second one is refused rather than silently taking the first one's place.
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

        if (request.WarehouseId is { } warehouseId)
        {
            if (await _warehouseRepository.GetByIdAsync(request.UserId, warehouseId, cancellationToken) is null)
            {
                return false;
            }

            if (await IsAlreadyMeasuredAgainstAnotherListAsync(request, warehouseId, cancellationToken))
            {
                return false;
            }
        }

        taskList.LinkToWarehouse(request.WarehouseId);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);
        return true;
    }

    /// <summary>
    /// Whether some other list of this account's already points at that warehouse. Pointing the same
    /// list at it again is not "another list" and is allowed - it is the state the caller is asking for.
    /// </summary>
    private async Task<bool> IsAlreadyMeasuredAgainstAnotherListAsync(
        LinkTaskListToWarehouseCommand request, Guid warehouseId, CancellationToken cancellationToken)
    {
        var everyList = await _taskRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        return everyList.Any(candidate =>
            candidate.Id != request.TaskListId && candidate.LinkedWarehouseId == warehouseId);
    }
}
