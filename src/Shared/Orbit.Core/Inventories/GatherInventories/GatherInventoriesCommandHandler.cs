using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.GatherInventories;

/// <summary>
/// Only the owner arranges a group, and only out of shelves of their own - the same two checks
/// MoveInventoryToFolderCommandHandler makes, for the same reasons. One row is one shelf, so a recipient
/// gathering it would be rearranging its owner's own page; and gathering somebody else's shelf into
/// yours would put rows on your page that they can take away without knowing.
/// </summary>
public sealed class GatherInventoriesCommandHandler : IRequestHandler<GatherInventoriesCommand, bool>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly InventoryGroups _groups;

    public GatherInventoriesCommandHandler(IInventoryRepository inventoryRepository, InventoryGroups groups)
    {
        _inventoryRepository = inventoryRepository;
        _groups = groups;
    }

    public async Task<bool> HandleAsync(GatherInventoriesCommand request, CancellationToken cancellationToken)
    {
        var inventory = await _inventoryRepository.GetByIdAsync(request.UserId, request.InventoryId, cancellationToken);
        if (inventory is null || inventory.UserId != request.UserId)
        {
            return false;
        }

        // A ring, or a shelf that is not theirs - see InventoryGroups.MayGatherAsync. Refused rather
        // than quietly trimmed: the caller asked for a membership, and saving a different one would be
        // answering a question nobody put.
        if (!await _groups.MayGatherAsync(
            request.UserId, request.InventoryId, request.InventoryIds, cancellationToken))
        {
            return false;
        }

        if (inventory.Gather(request.InventoryIds))
        {
            await _inventoryRepository.UpdateAsync(inventory, cancellationToken);
        }

        return true;
    }
}
