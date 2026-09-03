using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.GetInventories;

/// <summary>
/// <paramref name="UpdatedSinceUtc"/> narrows this to what changed at or after that instant, applied in
/// the database - what a client catching up asks for. Null means the whole list.
/// </summary>
public sealed record GetInventoriesQuery(Guid UserId, DateTimeOffset? UpdatedSinceUtc = null) : IRequest<IReadOnlyList<Inventory>>;
