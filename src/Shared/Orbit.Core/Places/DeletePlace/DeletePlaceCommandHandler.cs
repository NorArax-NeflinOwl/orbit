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
    private readonly ISyncTombstoneRepository _syncTombstoneRepository;

    public DeletePlaceCommandHandler(
        IPlaceRepository placeRepository, ISyncTombstoneRepository syncTombstoneRepository)
    {
        _placeRepository = placeRepository;
        _syncTombstoneRepository = syncTombstoneRepository;
    }

    public async Task<bool> HandleAsync(DeletePlaceCommand request, CancellationToken cancellationToken)
    {
        if (!await _placeRepository.DeleteAsync(request.UserId, request.Id, cancellationToken))
        {
            return false;
        }

        await _syncTombstoneRepository.RecordAsync(
            new SyncTombstone(request.UserId, SyncEntityType.Place, request.Id, DateTimeOffset.UtcNow),
            cancellationToken);
        return true;
    }
}
