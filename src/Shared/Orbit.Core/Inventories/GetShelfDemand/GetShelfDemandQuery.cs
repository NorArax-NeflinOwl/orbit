using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.GetShelfDemand;

/// <summary>
/// Which task entries ask for each item on this shelf - see ShelfDemand. What a reader editing an
/// amount needs before they press Save: whether changing it will reach a list, and which lists it would
/// have to choose between if it would reach several.
///
/// Returns null when the caller cannot see the inventory at all, as opposed to an empty list for a
/// shelf nothing is asking for - the same distinction GetInventoryItemsQuery draws.
/// </summary>
public sealed record GetShelfDemandQuery(Guid UserId, Guid InventoryId) : IRequest<IReadOnlyList<ShelfClaim>?>;
