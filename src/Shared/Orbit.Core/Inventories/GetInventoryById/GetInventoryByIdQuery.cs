using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.GetInventoryById;

public sealed record GetInventoryByIdQuery(Guid UserId, Guid InventoryId) : IRequest<Inventory?>;
