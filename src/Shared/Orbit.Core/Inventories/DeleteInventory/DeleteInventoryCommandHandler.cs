using Orbit.Core.Abstractions;
using Orbit.Core.Sync;

namespace Orbit.Core.Inventories.DeleteInventory;

public sealed class DeleteInventoryCommandHandler : IRequestHandler<DeleteInventoryCommand, bool>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IInventoryShareRepository _inventoryShareRepository;
    private readonly ISyncTombstoneRepository _syncTombstoneRepository;

    public DeleteInventoryCommandHandler(
        IInventoryRepository inventoryRepository, IInventoryItemRepository inventoryItemRepository,
        IInventoryShareRepository inventoryShareRepository, ISyncTombstoneRepository syncTombstoneRepository)
    {
        _inventoryRepository = inventoryRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _inventoryShareRepository = inventoryShareRepository;
        _syncTombstoneRepository = syncTombstoneRepository;
    }

    /// <summary>
    /// Only the owner can delete an inventory - not even a CanEdit grantee, since that would let a
    /// recipient destroy the owner's data wholesale rather than just edit it. A recipient pressing the
    /// same button takes it off their own list instead, by dropping their grant. The items inside go with
    /// it: an item has no owner of its own, so leaving them behind would strand rows nothing can reach.
    /// Accepted shares of a deleted inventory are left as dangling grants, which InventoryAccessResolver
    /// already reads as "not found" - matching how the rest of the codebase treats stale references.
    /// </summary>
    public async Task<bool> HandleAsync(DeleteInventoryCommand request, CancellationToken cancellationToken)
    {
        var inventory = await _inventoryRepository.GetByIdAsync(request.UserId, request.InventoryId, cancellationToken);
        if (inventory is null)
        {
            // Not the owner's. A recipient asking to be rid of something shared with them means
            // taking it off their own list - destroying somebody else's inventory is not theirs to
            // do. Removing the accepted grant does exactly that and leaves the owner's untouched.
            if (await _inventoryShareRepository.FindAcceptedGrantAsync(request.InventoryId, request.UserId, cancellationToken) is null)
            {
                return false;
            }

            await _inventoryShareRepository.RemoveAcceptedGrantAsync(request.InventoryId, request.UserId, cancellationToken);
            await RecordTombstoneAsync(request, cancellationToken);
            return true;
        }

        await _inventoryItemRepository.DeleteAllInInventoryAsync(request.InventoryId, cancellationToken);
        await _inventoryRepository.DeleteAsync(request.UserId, request.InventoryId, cancellationToken);
        await RecordTombstoneAsync(request, cancellationToken);
        return true;
    }

    /// <summary>
    /// Tombstones are per-user, which is what lets a dropped grant leave one: the inventory is gone
    /// from this reader's list and from nobody else's, and that is exactly what their next delta
    /// needs to say.
    ///
    /// Only the inventory gets one: its items are reached through it, so a client dropping the inventory
    /// drops them with it - see GET /api/inventories/{inventoryId}/items.
    /// </summary>
    private Task RecordTombstoneAsync(DeleteInventoryCommand request, CancellationToken cancellationToken)
        => _syncTombstoneRepository.RecordAsync(
            new SyncTombstone(request.UserId, SyncEntityType.Inventory, request.InventoryId, DateTimeOffset.UtcNow),
            cancellationToken);
}
