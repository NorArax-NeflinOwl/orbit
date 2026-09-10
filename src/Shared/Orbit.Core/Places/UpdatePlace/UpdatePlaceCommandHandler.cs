using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.UpdatePlace;

public sealed class UpdatePlaceCommandHandler : IRequestHandler<UpdatePlaceCommand, bool>
{
    private readonly PlaceAccessResolver _placeAccessResolver;
    private readonly IPlaceRepository _placeRepository;

    public UpdatePlaceCommandHandler(PlaceAccessResolver placeAccessResolver, IPlaceRepository placeRepository)
    {
        _placeAccessResolver = placeAccessResolver;
        _placeRepository = placeRepository;
    }

    public async Task<bool> HandleAsync(UpdatePlaceCommand request, CancellationToken cancellationToken)
    {
        if (await _placeAccessResolver.ResolveAsync(request.UserId, request.Id, cancellationToken) is not { } place)
        {
            return false;
        }

        // Read-only means read-only: somebody handed a place to look at may not rewrite what they were
        // handed. Refused here as well as greyed out in the client, so a hand-made request cannot do
        // what the screen will not.
        if (place.AccessLevel != ShareAccessLevel.CanEdit)
        {
            throw new InvalidRequestException("You can only read this place.");
        }

        place.Update(
            request.Name, request.Description, request.Where, request.Colour, request.Priority,
            request.TaskListIds, request.IsPrivate, request.EncryptedContent);
        await _placeRepository.UpdateAsync(place, cancellationToken);
        return true;
    }
}
