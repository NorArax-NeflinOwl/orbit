using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.DeleteWarehouse;

public sealed class DeleteWarehouseCommandHandler : IRequestHandler<DeleteWarehouseCommand, bool>
{
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IWarehouseShareRepository _warehouseShareRepository;

    public DeleteWarehouseCommandHandler(
        IWarehouseRepository warehouseRepository, IInventoryRepository inventoryRepository,
        IWarehouseShareRepository warehouseShareRepository)
    {
        _warehouseRepository = warehouseRepository;
        _inventoryRepository = inventoryRepository;
        _warehouseShareRepository = warehouseShareRepository;
    }

    /// <summary>
    /// Only the owner can delete a warehouse - not even a CanEdit grantee, since that would let a
    /// recipient destroy the owner's data wholesale rather than just edit it. A recipient pressing the
    /// same button takes it off their own list instead, by dropping their grant. The items inside go with
    /// it: an item has no owner of its own, so leaving them behind would strand rows nothing can reach.
    /// Accepted shares of a deleted warehouse are left as dangling grants, which WarehouseAccessResolver
    /// already reads as "not found" - matching how the rest of the codebase treats stale references.
    /// </summary>
    public async Task<bool> HandleAsync(DeleteWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await _warehouseRepository.GetByIdAsync(request.UserId, request.WarehouseId, cancellationToken);
        if (warehouse is null)
        {
            // Not the owner's. A recipient asking to be rid of something shared with them means
            // taking it off their own list - destroying somebody else's warehouse is not theirs to
            // do. Removing the accepted grant does exactly that and leaves the owner's untouched.
            if (await _warehouseShareRepository.FindAcceptedGrantAsync(request.WarehouseId, request.UserId, cancellationToken) is not null)
            {
                await _warehouseShareRepository.RemoveAcceptedGrantAsync(request.WarehouseId, request.UserId, cancellationToken);
                return true;
            }

            return false;
        }

        await _inventoryRepository.DeleteAllInWarehouseAsync(request.WarehouseId, cancellationToken);
        await _warehouseRepository.DeleteAsync(request.UserId, request.WarehouseId, cancellationToken);
        return true;
    }
}
