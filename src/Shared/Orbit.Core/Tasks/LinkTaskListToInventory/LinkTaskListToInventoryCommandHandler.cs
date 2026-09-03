using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;

namespace Orbit.Core.Tasks.LinkTaskListToInventory;

/// <summary>
/// Only the list's owner may point it at an inventory, and only at an inventory they can actually read -
/// otherwise the stock check would report on shelves its reader never had access to.
///
/// Several lists may share one inventory. That used to be refused, because two lists measured against
/// one shelf each reported a shortfall the other had already accounted for; what answers that is the
/// counting rather than a ban - a shelf is now measured against everything asking for it at once, and
/// each list is told its share (see GetTaskListStockCheckQueryHandler). A list still points at one
/// inventory: work is done out of one store.
/// </summary>
public sealed class LinkTaskListToInventoryCommandHandler : IRequestHandler<LinkTaskListToInventoryCommand, bool>
{
    private readonly ITaskRepository _taskRepository;
    private readonly IInventoryRepository _inventoryRepository;

    public LinkTaskListToInventoryCommandHandler(ITaskRepository taskRepository, IInventoryRepository inventoryRepository)
    {
        _taskRepository = taskRepository;
        _inventoryRepository = inventoryRepository;
    }

    public async Task<bool> HandleAsync(LinkTaskListToInventoryCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskRepository.GetByIdAsync(request.UserId, request.TaskListId, cancellationToken);
        if (taskList is null || taskList.UserId != request.UserId)
        {
            return false;
        }

        if (request.InventoryId is { } inventoryId
            && await _inventoryRepository.GetByIdAsync(request.UserId, inventoryId, cancellationToken) is null)
        {
            return false;
        }

        taskList.LinkToInventory(request.InventoryId);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);
        return true;
    }
}
