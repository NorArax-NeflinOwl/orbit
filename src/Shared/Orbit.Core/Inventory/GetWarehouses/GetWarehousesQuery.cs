using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.GetWarehouses;

public sealed record GetWarehousesQuery(Guid UserId) : IRequest<IReadOnlyList<Warehouse>>;
