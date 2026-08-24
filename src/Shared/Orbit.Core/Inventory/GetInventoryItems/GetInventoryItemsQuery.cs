using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.GetInventoryItems;

public sealed record GetInventoryItemsQuery(Guid UserId) : IRequest<IReadOnlyList<InventoryItem>>;
