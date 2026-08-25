using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.GetInventoryItemById;

public sealed record GetInventoryItemByIdQuery(Guid UserId, Guid WarehouseId, Guid Id) : IRequest<InventoryItem?>;
