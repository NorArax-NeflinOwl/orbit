using Orbit.Core.Abstractions;

namespace Orbit.Core.Location.GetSharedLocations;

/// <summary>Positions shared with UserId - what the recipient polls to see where people are.</summary>
public sealed record GetSharedLocationsQuery(Guid UserId) : IRequest<IReadOnlyList<SharedLocation>>;

/// <summary>Positions UserId is currently sharing, so they can see who can see them and end any of it.</summary>
public sealed record GetOwnLocationSharesQuery(Guid UserId) : IRequest<IReadOnlyList<SharedLocation>>;
