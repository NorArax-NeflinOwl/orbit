using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.DuplicatePlace;

public sealed class DuplicatePlaceCommandHandler : IRequestHandler<DuplicatePlaceCommand, Guid?>
{
    private readonly IPlaceRepository _placeRepository;

    public DuplicatePlaceCommandHandler(IPlaceRepository placeRepository)
    {
        _placeRepository = placeRepository;
    }

    /// <summary>
    /// Everything about the place travels, the lists it belongs to included: a copy of the bakery is
    /// still the shopping list's bakery. What does not is the id and the two timestamps, which are the
    /// copy's own from the moment it exists.
    /// </summary>
    public async Task<Guid?> HandleAsync(DuplicatePlaceCommand request, CancellationToken cancellationToken)
    {
        if (await _placeRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken) is not { } place)
        {
            return null;
        }

        var copy = place.CopyFor(place.UserId, request.Name ?? place.Name);
        await _placeRepository.AddAsync(copy, cancellationToken);
        return copy.Id;
    }
}
