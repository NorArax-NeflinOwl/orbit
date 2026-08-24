using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.DeleteInventoryItem;

public sealed class DeleteInventoryItemCommandHandler : IRequestHandler<DeleteInventoryItemCommand, bool>
{
    private readonly IInventoryRepository _inventoryRepository;

    public DeleteInventoryItemCommandHandler(IInventoryRepository inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;
    }

    /// <summary>
    /// Returns false instead of throwing when the item is missing or not owned by the requesting user.
    /// Deliberately does not touch its linked restock TaskItem, if any - leaving it behind is
    /// consistent with restock tasks being first-class Tasks entries once created (see
    /// InventoryTaskListCoordinator), not something Inventory reaches back to delete.
    /// </summary>
    public async Task<bool> HandleAsync(DeleteInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _inventoryRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
        if (item is null)
        {
            return false;
        }

        await _inventoryRepository.DeleteAsync(request.UserId, request.Id, cancellationToken);
        return true;
    }
}
