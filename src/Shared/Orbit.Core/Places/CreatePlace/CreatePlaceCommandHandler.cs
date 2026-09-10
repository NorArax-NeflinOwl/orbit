using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.CreatePlace;

public sealed class CreatePlaceCommandHandler : IRequestHandler<CreatePlaceCommand, Guid>
{
    private readonly IPlaceRepository _placeRepository;

    public CreatePlaceCommandHandler(IPlaceRepository placeRepository)
    {
        _placeRepository = placeRepository;
    }

    public async Task<Guid> HandleAsync(CreatePlaceCommand request, CancellationToken cancellationToken)
    {
        var place = Place.Create(
            request.UserId, request.Name, request.Description, request.Where, request.Colour,
            request.Priority, request.TaskListIds, request.IsPrivate, request.EncryptedContent);
        await _placeRepository.AddAsync(place, cancellationToken);
        return place.Id;
    }
}
