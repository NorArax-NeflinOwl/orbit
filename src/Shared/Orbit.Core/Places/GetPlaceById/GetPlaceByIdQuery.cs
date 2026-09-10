using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.GetPlaceById;

public sealed record GetPlaceByIdQuery(Guid UserId, Guid Id) : IRequest<Place?>;
