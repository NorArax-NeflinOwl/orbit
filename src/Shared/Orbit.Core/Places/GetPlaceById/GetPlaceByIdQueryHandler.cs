using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.GetPlaceById;

public sealed class GetPlaceByIdQueryHandler : IRequestHandler<GetPlaceByIdQuery, Place?>
{
    private readonly IPlaceRepository _placeRepository;

    public GetPlaceByIdQueryHandler(IPlaceRepository placeRepository)
    {
        _placeRepository = placeRepository;
    }

    public Task<Place?> HandleAsync(GetPlaceByIdQuery request, CancellationToken cancellationToken)
        => _placeRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
}
