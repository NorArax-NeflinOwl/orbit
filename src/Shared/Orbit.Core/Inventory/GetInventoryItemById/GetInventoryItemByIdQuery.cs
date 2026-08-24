using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.GetInventoryItemById;

public sealed record GetInventoryItemByIdQuery(Guid UserId, Guid Id) : IRequest<InventoryItem?>;
