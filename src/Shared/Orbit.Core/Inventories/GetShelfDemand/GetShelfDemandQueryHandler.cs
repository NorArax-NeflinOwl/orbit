using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.GetShelfDemand;

public sealed class GetShelfDemandQueryHandler(
    InventoryAccessResolver inventoryAccessResolver,
    IInventoryItemRepository inventoryItemRepository,
    ShelfDemand shelfDemand) : IRequestHandler<GetShelfDemandQuery, IReadOnlyList<ShelfClaim>?>
{
    /// <summary>
    /// Read-only access is enough, as it is for listing the items themselves: this says what is already
    /// true of the caller's own lists, and somebody who may read the shelf may read that.
    ///
    /// The lists searched are the caller's, not the inventory's owner's. A shared shelf is measured
    /// against the lists of whoever is looking at it - the same reader ShelfUsage counts for - so this
    /// answers "which of <b>your</b> lists ask for this", which is the question the warning is about.
    /// </summary>
    public async Task<IReadOnlyList<ShelfClaim>?> HandleAsync(
        GetShelfDemandQuery request, CancellationToken cancellationToken)
    {
        if (await inventoryAccessResolver.ResolveAsync(request.UserId, request.InventoryId, cancellationToken) is null)
        {
            return null;
        }

        var shelfItemIds = (await inventoryItemRepository.GetAllAsync(request.InventoryId, cancellationToken))
            .Select(item => item.Id)
            .ToHashSet();

        return await shelfDemand.WhoAsksForAsync(request.UserId, shelfItemIds, cancellationToken);
    }
}
