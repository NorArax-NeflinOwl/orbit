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
///
/// Linking puts the list's product entries onto that shelf there and then, the way a save of the list
/// does (see ProductEntryPlacement). It used to set the link and nothing else, so the products a list
/// already named reached the shelf only on its next save - a list linked and left alone had its products
/// on no shelf at all, which is the opposite of what linking it says.
/// </summary>
public sealed class LinkTaskListToInventoryCommandHandler : IRequestHandler<LinkTaskListToInventoryCommand, bool>
{
    private readonly ITaskRepository _taskRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ProductEntryPlacement _productEntryPlacement;

    public LinkTaskListToInventoryCommandHandler(
        ITaskRepository taskRepository, IInventoryRepository inventoryRepository, ProductEntryPlacement productEntryPlacement)
    {
        _taskRepository = taskRepository;
        _inventoryRepository = inventoryRepository;
        _productEntryPlacement = productEntryPlacement;
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
        // The list's own entries are both what is stored and what is placed: nothing new arrives with a
        // link, and an entry already standing for a row is recognised as one and left alone.
        var placedOnInventoryId = await _productEntryPlacement.PlaceAsync(
            request.UserId, taskList, [.. taskList.Items], taskList.IsPrivate, cancellationToken);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);

        if (placedOnInventoryId is { } placedOn)
        {
            await _productEntryPlacement.SettleTheRestockListAsync(placedOn, cancellationToken);
        }

        return true;
    }
}
