using Orbit.Core.Abstractions;
using Orbit.Core.Folders;

namespace Orbit.Core.Places.MovePlaceToFolder;

/// <summary>
/// Only the place's owner files it, and only into a folder of their own - the two checks
/// MoveCalendarEventToFolderCommandHandler makes, and for the same reasons: a folder id arrives from a
/// client, and one belonging to somebody else would file this place under a tab its owner cannot see,
/// which reads as the place having gone off the map.
///
/// A place somebody was handed is refused as well, and the first check is what refuses it: filing is the
/// owner's own arrangement of their own map (see <see cref="Place.FolderId"/>), and the row is the
/// owner's.
/// </summary>
public sealed class MovePlaceToFolderCommandHandler : IRequestHandler<MovePlaceToFolderCommand, bool>
{
    private readonly IPlaceRepository _placeRepository;
    private readonly IFolderRepository _folderRepository;

    public MovePlaceToFolderCommandHandler(IPlaceRepository placeRepository, IFolderRepository folderRepository)
    {
        _placeRepository = placeRepository;
        _folderRepository = folderRepository;
    }

    public async Task<bool> HandleAsync(MovePlaceToFolderCommand request, CancellationToken cancellationToken)
    {
        var place = await _placeRepository.GetByIdAsync(request.UserId, request.PlaceId, cancellationToken);
        if (place is null || place.UserId != request.UserId)
        {
            return false;
        }

        if (request.FolderId is { } folderId
            && await _folderRepository.GetByIdAsync(request.UserId, folderId, cancellationToken) is null)
        {
            return false;
        }

        place.MoveToFolder(request.FolderId);
        await _placeRepository.UpdateAsync(place, cancellationToken);
        return true;
    }
}
