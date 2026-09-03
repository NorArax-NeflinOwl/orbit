using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.GetInventoryItems;

/// <summary>Returns null when the caller has no access to InventoryId at all, as opposed to an empty list for an inventory they can see that simply has no items.</summary>
public sealed record GetInventoryItemsQuery(Guid UserId, Guid InventoryId) : IRequest<IReadOnlyList<InventoryItem>?>;
