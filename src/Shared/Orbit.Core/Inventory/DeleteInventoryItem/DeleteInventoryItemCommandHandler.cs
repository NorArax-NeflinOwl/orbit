using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.DeleteInventoryItem;

public sealed class DeleteInventoryItemCommandHandler : IRequestHandler<DeleteInventoryItemCommand, bool>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly WarehouseAccessResolver _warehouseAccessResolver;

    public DeleteInventoryItemCommandHandler(IInventoryRepository inventoryRepository, WarehouseAccessResolver warehouseAccessResolver)
    {
        _inventoryRepository = inventoryRepository;
        _warehouseAccessResolver = warehouseAccessResolver;
    }

    /// <summary>
    /// Returns false instead of throwing when the item is missing, or its warehouse isn't writable by
    /// this caller. Deliberately does not touch its linked restock TaskItem, if any - leaving it behind
    /// is consistent with restock tasks being first-class Tasks entries once created (see
    /// InventoryTaskListCoordinator), not something Inventory reaches back to delete.
    /// </summary>
    public async Task<bool> HandleAsync(DeleteInventoryItemCommand request, CancellationToken cancellationToken)
    {
        if (await _warehouseAccessResolver.ResolveForEditAsync(request.UserId, request.WarehouseId, cancellationToken) is null)
        {
            return false;
        }

        var item = await _inventoryRepository.GetByIdAsync(request.WarehouseId, request.Id, cancellationToken);
        if (item is null)
        {
            return false;
        }

        await _inventoryRepository.DeleteAsync(request.WarehouseId, request.Id, cancellationToken);
        return true;
    }
}
