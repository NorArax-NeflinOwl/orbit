using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.DeleteWarehouse;

public sealed record DeleteWarehouseCommand(Guid UserId, Guid WarehouseId) : IRequest<bool>;
