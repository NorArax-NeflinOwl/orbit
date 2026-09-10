using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.GetPlaceById;

public sealed class GetPlaceByIdQueryHandler : IRequestHandler<GetPlaceByIdQuery, Place?>
{
    private readonly PlaceAccessResolver _placeAccessResolver;

    public GetPlaceByIdQueryHandler(PlaceAccessResolver placeAccessResolver)
    {
        _placeAccessResolver = placeAccessResolver;
    }

    /// <summary>Null when the caller neither keeps this place nor holds an accepted share of it.</summary>
    public Task<Place?> HandleAsync(GetPlaceByIdQuery request, CancellationToken cancellationToken)
        => _placeAccessResolver.ResolveAsync(request.UserId, request.Id, cancellationToken);
}
