using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.GetInventoryItems;

/// <summary>Returns null when the caller has no access to WarehouseId at all, as opposed to an empty list for a warehouse they can see that simply has no items.</summary>
public sealed record GetInventoryItemsQuery(Guid UserId, Guid WarehouseId) : IRequest<IReadOnlyList<InventoryItem>?>;
