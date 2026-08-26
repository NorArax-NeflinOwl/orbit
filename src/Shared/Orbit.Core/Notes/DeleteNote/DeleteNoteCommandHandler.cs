using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.DeleteNote;

public sealed class DeleteNoteCommandHandler : IRequestHandler<DeleteNoteCommand, bool>
{
    private readonly INoteRepository _noteRepository;
    private readonly INoteShareRepository _noteShareRepository;

    public DeleteNoteCommandHandler(
        INoteRepository noteRepository, INoteShareRepository noteShareRepository)
    {
        _noteRepository = noteRepository;
        _noteShareRepository = noteShareRepository;
    }

    /// <summary>
    /// Deletes the caller's own note, or - when it is somebody else's, shared with them - takes it off
    /// their list by dropping the grant. False when it is neither, so the API answers 404 without
    /// leaking which of the two it was.
    /// </summary>
    public async Task<bool> HandleAsync(DeleteNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await _noteRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
        if (note is null)
        {
            // Not the owner's. A recipient asking to be rid of something shared with them means
            // taking it off their own list - destroying somebody else's note is not theirs to
            // do. Removing the accepted grant does exactly that and leaves the owner's untouched.
            if (await _noteShareRepository.FindAcceptedGrantAsync(request.Id, request.UserId, cancellationToken) is not null)
            {
                await _noteShareRepository.RemoveAcceptedGrantAsync(request.Id, request.UserId, cancellationToken);
                return true;
            }

            return false;
        }

        await _noteRepository.DeleteAsync(request.UserId, request.Id, cancellationToken);
        return true;
    }
}
