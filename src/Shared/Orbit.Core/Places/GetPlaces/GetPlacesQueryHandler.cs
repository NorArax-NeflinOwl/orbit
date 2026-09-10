using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.GetPlaces;

public sealed class GetPlacesQueryHandler : IRequestHandler<GetPlacesQuery, IReadOnlyList<Place>>
{
    private readonly IPlaceRepository _placeRepository;

    public GetPlacesQueryHandler(IPlaceRepository placeRepository)
    {
        _placeRepository = placeRepository;
    }

    public Task<IReadOnlyList<Place>> HandleAsync(GetPlacesQuery request, CancellationToken cancellationToken)
        => _placeRepository.GetAllAsync(request.UserId, request.UpdatedSinceUtc, cancellationToken);
}
