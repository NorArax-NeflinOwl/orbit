using Orbit.Core.Abstractions;
using Orbit.Core.Sync;

namespace Orbit.Core.Places.DeletePlace;

/// <summary>
/// Writes a tombstone as well as deleting, so a client holding its own copy learns the place is gone
/// rather than keeping it forever - the same thing deleting a note, a list, an event or an inventory
/// does. See ISyncTombstoneRepository.
/// </summary>
public sealed class DeletePlaceCommandHandler : IRequestHandler<DeletePlaceCommand, bool>
{
    private readonly IPlaceRepository _placeRepository;
    private readonly IPlaceShareRepository _placeShareRepository;
    private readonly ISyncTombstoneRepository _syncTombstoneRepository;

    public DeletePlaceCommandHandler(
        IPlaceRepository placeRepository, IPlaceShareRepository placeShareRepository,
        ISyncTombstoneRepository syncTombstoneRepository)
    {
        _placeRepository = placeRepository;
        _placeShareRepository = placeShareRepository;
        _syncTombstoneRepository = syncTombstoneRepository;
    }

    /// <summary>
    /// Deletes the caller's own place, or - when it is somebody else's, handed to them - takes it off
    /// their own map by dropping the grant. Destroying a place somebody else keeps is not theirs to do,
    /// and the two answer the same way, so the API says 404 without saying which it was.
    ///
    /// The tombstone is per-user, which is what lets a dropped grant leave one: the place is gone from
    /// this reader's map and from nobody else's, and that is what their next delta has to say.
    /// </summary>
    public async Task<bool> HandleAsync(DeletePlaceCommand request, CancellationToken cancellationToken)
    {
        if (!await _placeRepository.DeleteAsync(request.UserId, request.Id, cancellationToken))
        {
            if (await _placeShareRepository.FindAcceptedGrantAsync(request.Id, request.UserId, cancellationToken) is null)
            {
                return false;
            }

            await _placeShareRepository.RemoveAcceptedGrantAsync(request.Id, request.UserId, cancellationToken);
        }

        await _syncTombstoneRepository.RecordAsync(
            new SyncTombstone(request.UserId, SyncEntityType.Place, request.Id, DateTimeOffset.UtcNow),
            cancellationToken);
        return true;
    }
}
