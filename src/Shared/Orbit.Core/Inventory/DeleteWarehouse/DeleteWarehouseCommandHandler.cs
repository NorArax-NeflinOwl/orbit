using Orbit.Core.Abstractions;
using Orbit.Core.Sync;

namespace Orbit.Core.Inventory.DeleteWarehouse;

public sealed class DeleteWarehouseCommandHandler : IRequestHandler<DeleteWarehouseCommand, bool>
{
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IWarehouseShareRepository _warehouseShareRepository;
    private readonly ISyncTombstoneRepository _syncTombstoneRepository;

    public DeleteWarehouseCommandHandler(
        IWarehouseRepository warehouseRepository, IInventoryRepository inventoryRepository,
        IWarehouseShareRepository warehouseShareRepository, ISyncTombstoneRepository syncTombstoneRepository)
    {
        _warehouseRepository = warehouseRepository;
        _inventoryRepository = inventoryRepository;
        _warehouseShareRepository = warehouseShareRepository;
        _syncTombstoneRepository = syncTombstoneRepository;
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
            if (await _warehouseShareRepository.FindAcceptedGrantAsync(request.WarehouseId, request.UserId, cancellationToken) is null)
            {
                return false;
            }

            await _warehouseShareRepository.RemoveAcceptedGrantAsync(request.WarehouseId, request.UserId, cancellationToken);
            await RecordTombstoneAsync(request, cancellationToken);
            return true;
        }

        await _inventoryRepository.DeleteAllInWarehouseAsync(request.WarehouseId, cancellationToken);
        await _warehouseRepository.DeleteAsync(request.UserId, request.WarehouseId, cancellationToken);
        await RecordTombstoneAsync(request, cancellationToken);
        return true;
    }

    /// <summary>
    /// Tombstones are per-user, which is what lets a dropped grant leave one: the warehouse is gone
    /// from this reader's list and from nobody else's, and that is exactly what their next delta
    /// needs to say.
    ///
    /// Only the warehouse gets one: its items are reached through it, so a client dropping the warehouse
    /// drops them with it - see GET /api/warehouses/{warehouseId}/items.
    /// </summary>
    private Task RecordTombstoneAsync(DeleteWarehouseCommand request, CancellationToken cancellationToken)
        => _syncTombstoneRepository.RecordAsync(
            new SyncTombstone(request.UserId, SyncEntityType.Warehouse, request.WarehouseId, DateTimeOffset.UtcNow),
            cancellationToken);
}
