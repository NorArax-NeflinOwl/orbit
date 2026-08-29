using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.GetWarehouses;

/// <summary>
/// <paramref name="UpdatedSinceUtc"/> narrows this to what changed at or after that instant, applied in
/// the database - what a client catching up asks for. Null means the whole list.
/// </summary>
public sealed record GetWarehousesQuery(Guid UserId, DateTimeOffset? UpdatedSinceUtc = null) : IRequest<IReadOnlyList<Warehouse>>;
