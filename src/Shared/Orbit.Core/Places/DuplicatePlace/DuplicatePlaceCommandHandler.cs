using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.DuplicatePlace;

public sealed class DuplicatePlaceCommandHandler : IRequestHandler<DuplicatePlaceCommand, Guid?>
{
    private readonly PlaceAccessResolver _placeAccessResolver;
    private readonly IPlaceRepository _placeRepository;

    public DuplicatePlaceCommandHandler(PlaceAccessResolver placeAccessResolver, IPlaceRepository placeRepository)
    {
        _placeAccessResolver = placeAccessResolver;
        _placeRepository = placeRepository;
    }

    /// <summary>
    /// Everything about the place travels, the lists it belongs to included: a copy of the bakery is
    /// still the shopping list's bakery. What does not is the id and the two timestamps, which are the
    /// copy's own from the moment it exists.
    ///
    /// A place somebody handed over can be copied too, and the copy is the *caller's* - keeping one for
    /// yourself is what a reader does with a place they were shown, and it must not become a second
    /// place on the sharer's own map.
    /// </summary>
    public async Task<Guid?> HandleAsync(DuplicatePlaceCommand request, CancellationToken cancellationToken)
    {
        if (await _placeAccessResolver.ResolveAsync(request.UserId, request.Id, cancellationToken) is not { } place)
        {
            return null;
        }

        var copy = place.CopyFor(request.UserId, request.Name ?? place.Name);
        await _placeRepository.AddAsync(copy, cancellationToken);
        return copy.Id;
    }
}
