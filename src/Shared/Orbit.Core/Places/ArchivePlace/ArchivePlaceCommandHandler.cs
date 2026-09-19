using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.ArchivePlace;

/// <summary>
/// Only the owner puts a place away, and only their own - the rule ArchiveNoteCommandHandler follows,
/// for the same reason: one row is one place, so archiving one shared with this reader would take it
/// off the map of the person who keeps it. Somebody handed a place they are finished with takes it off
/// their own map instead, which is what DeletePlaceCommand does with a grant.
/// </summary>
public sealed class ArchivePlaceCommandHandler : IRequestHandler<ArchivePlaceCommand, bool>
{
    private readonly IPlaceRepository _places;

    public ArchivePlaceCommandHandler(IPlaceRepository places) => _places = places;

    public async Task<bool> HandleAsync(ArchivePlaceCommand request, CancellationToken cancellationToken)
    {
        var found = await _places.GetByIdAsync(request.UserId, request.PlaceId, cancellationToken);
        if (found is null || found.UserId != request.UserId)
        {
            return false;
        }

        found.Archive(request.IsArchived);
        await _places.UpdateAsync(found, cancellationToken);
        return true;
    }
}
