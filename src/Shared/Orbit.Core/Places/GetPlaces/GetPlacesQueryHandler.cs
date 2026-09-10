using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.GetPlaces;

/// <summary>
/// Every place the caller keeps, plus every place somebody handed them and they took up - see
/// PlaceAccessResolver, which is what stamps each one with how this reader relates to it.
/// </summary>
public sealed class GetPlacesQueryHandler : IRequestHandler<GetPlacesQuery, IReadOnlyList<Place>>
{
    private readonly PlaceAccessResolver _placeAccessResolver;

    public GetPlacesQueryHandler(PlaceAccessResolver placeAccessResolver)
    {
        _placeAccessResolver = placeAccessResolver;
    }

    public Task<IReadOnlyList<Place>> HandleAsync(GetPlacesQuery request, CancellationToken cancellationToken)
        => _placeAccessResolver.ResolveAllAsync(request.UserId, request.UpdatedSinceUtc, cancellationToken);
}
