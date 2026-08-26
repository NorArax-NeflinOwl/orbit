using Orbit.Core.Abstractions;
using Orbit.Core.Sync;

namespace Orbit.Core.Notes.DeleteNote;

public sealed class DeleteNoteCommandHandler : IRequestHandler<DeleteNoteCommand, bool>
{
    private readonly INoteRepository _noteRepository;
    private readonly ISyncTombstoneRepository _syncTombstoneRepository;

    public DeleteNoteCommandHandler(INoteRepository noteRepository, ISyncTombstoneRepository syncTombstoneRepository)
    {
        _noteRepository = noteRepository;
        _syncTombstoneRepository = syncTombstoneRepository;
    }

    /// <summary>
    /// Returns false instead of throwing when the note is missing or not owned by the requesting user,
    /// so the API can turn that into a 404 either way, without leaking which is the case.
    ///
    /// Leaves a tombstone behind so a client that was offline at the time still learns the note is gone -
    /// an absent row looks exactly like one the client already has (see SyncTombstone).
    /// </summary>
    public async Task<bool> HandleAsync(DeleteNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await _noteRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
        if (note is null)
        {
            return false;
        }

        await _noteRepository.DeleteAsync(request.UserId, request.Id, cancellationToken);
        await _syncTombstoneRepository.RecordAsync(
            new SyncTombstone(request.UserId, SyncEntityType.Note, request.Id, DateTimeOffset.UtcNow), cancellationToken);
        return true;
    }
}
