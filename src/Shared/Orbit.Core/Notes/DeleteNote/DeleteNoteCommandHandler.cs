using Orbit.Core.Abstractions;
using Orbit.Core.Sync;

namespace Orbit.Core.Notes.DeleteNote;

public sealed class DeleteNoteCommandHandler : IRequestHandler<DeleteNoteCommand, bool>
{
    private readonly INoteRepository _noteRepository;
    private readonly INoteShareRepository _noteShareRepository;
    private readonly ISyncTombstoneRepository _syncTombstoneRepository;

    public DeleteNoteCommandHandler(
        INoteRepository noteRepository, INoteShareRepository noteShareRepository,
        ISyncTombstoneRepository syncTombstoneRepository)
    {
        _noteRepository = noteRepository;
        _noteShareRepository = noteShareRepository;
        _syncTombstoneRepository = syncTombstoneRepository;
    }

    /// <summary>
    /// Deletes the caller's own note, or - when it is somebody else's, shared with them - takes it off
    /// their list by dropping the grant. False when it is neither, so the API answers 404 without
    /// leaking which of the two it was.
    ///
    /// Either way leaves a tombstone behind so a client that was offline at the time still learns the
    /// note is gone - an absent row looks exactly like one the client already has (see SyncTombstone).
    /// </summary>
    public async Task<bool> HandleAsync(DeleteNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await _noteRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
        if (note is null)
        {
            // Not the owner's. A recipient asking to be rid of something shared with them means
            // taking it off their own list - destroying somebody else's note is not theirs to
            // do. Removing the accepted grant does exactly that and leaves the owner's untouched.
            if (await _noteShareRepository.FindAcceptedGrantAsync(request.Id, request.UserId, cancellationToken) is null)
            {
                return false;
            }

            await _noteShareRepository.RemoveAcceptedGrantAsync(request.Id, request.UserId, cancellationToken);
            await RecordTombstoneAsync(request, cancellationToken);
            return true;
        }

        await _noteRepository.DeleteAsync(request.UserId, request.Id, cancellationToken);
        await RecordTombstoneAsync(request, cancellationToken);
        return true;
    }

    /// <summary>
    /// Tombstones are per-user, which is what lets a dropped grant leave one: the note is gone from this
    /// reader's list and from nobody else's, and that is exactly what their next delta needs to say.
    /// </summary>
    private Task RecordTombstoneAsync(DeleteNoteCommand request, CancellationToken cancellationToken)
        => _syncTombstoneRepository.RecordAsync(
            new SyncTombstone(request.UserId, SyncEntityType.Note, request.Id, DateTimeOffset.UtcNow), cancellationToken);
}
