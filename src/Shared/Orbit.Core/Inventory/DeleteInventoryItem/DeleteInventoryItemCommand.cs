using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.DeleteInventoryItem;

public sealed record DeleteInventoryItemCommand(Guid UserId, Guid WarehouseId, Guid Id) : IRequest<bool>;
