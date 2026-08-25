using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.DeleteWarehouse;

public sealed class DeleteWarehouseCommandHandler : IRequestHandler<DeleteWarehouseCommand, bool>
{
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryRepository _inventoryRepository;

    public DeleteWarehouseCommandHandler(IWarehouseRepository warehouseRepository, IInventoryRepository inventoryRepository)
    {
        _warehouseRepository = warehouseRepository;
        _inventoryRepository = inventoryRepository;
    }

    /// <summary>
    /// Only the owner can delete a warehouse - not even a CanEdit grantee, since that would let a
    /// recipient destroy the owner's data wholesale rather than just edit it. The items inside go with
    /// it: an item has no owner of its own, so leaving them behind would strand rows nothing can reach.
    /// Accepted shares of a deleted warehouse are left as dangling grants, which WarehouseAccessResolver
    /// already reads as "not found" - matching how the rest of the codebase treats stale references.
    /// </summary>
    public async Task<bool> HandleAsync(DeleteWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await _warehouseRepository.GetByIdAsync(request.UserId, request.WarehouseId, cancellationToken);
        if (warehouse is null)
        {
            return false;
        }

        await _inventoryRepository.DeleteAllInWarehouseAsync(request.WarehouseId, cancellationToken);
        await _warehouseRepository.DeleteAsync(request.UserId, request.WarehouseId, cancellationToken);
        return true;
    }
}
