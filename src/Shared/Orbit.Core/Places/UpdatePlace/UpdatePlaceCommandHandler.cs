using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.UpdatePlace;

public sealed class UpdatePlaceCommandHandler : IRequestHandler<UpdatePlaceCommand, bool>
{
    private readonly IPlaceRepository _placeRepository;

    public UpdatePlaceCommandHandler(IPlaceRepository placeRepository)
    {
        _placeRepository = placeRepository;
    }

    public async Task<bool> HandleAsync(UpdatePlaceCommand request, CancellationToken cancellationToken)
    {
        // Scoped to the owner by the read, which is the whole of the access check here: a place is not
        // shared with anybody yet, so "yours" is the only way to reach one.
        if (await _placeRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken) is not { } place)
        {
            return false;
        }

        place.Update(
            request.Name, request.Description, request.Where, request.Colour, request.Priority,
            request.TaskListIds);
        await _placeRepository.UpdateAsync(place, cancellationToken);
        return true;
    }
}
