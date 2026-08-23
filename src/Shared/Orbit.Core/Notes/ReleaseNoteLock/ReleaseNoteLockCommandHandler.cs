using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.ReleaseNoteLock;

public sealed class ReleaseNoteLockCommandHandler : IRequestHandler<ReleaseNoteLockCommand, bool>
{
    private readonly NoteAccessResolver _noteAccessResolver;
    private readonly INoteRepository _noteRepository;

    public ReleaseNoteLockCommandHandler(NoteAccessResolver noteAccessResolver, INoteRepository noteRepository)
    {
        _noteAccessResolver = noteAccessResolver;
        _noteRepository = noteRepository;
    }

    /// <summary>Returns false instead of throwing when noteId doesn't exist or isn't accessible to userId - releasing a lock on something you can't even see is a harmless no-op either way.</summary>
    public async Task<bool> HandleAsync(ReleaseNoteLockCommand request, CancellationToken cancellationToken)
    {
        var note = await _noteAccessResolver.ResolveAsync(request.UserId, request.NoteId, cancellationToken);
        if (note is null)
        {
            return false;
        }

        note.ReleaseLock(request.UserId);
        await _noteRepository.UpdateAsync(note, cancellationToken);
        return true;
    }
}
