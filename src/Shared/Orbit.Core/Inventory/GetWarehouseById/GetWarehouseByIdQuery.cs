using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.GetWarehouseById;

public sealed record GetWarehouseByIdQuery(Guid UserId, Guid WarehouseId) : IRequest<Warehouse?>;
