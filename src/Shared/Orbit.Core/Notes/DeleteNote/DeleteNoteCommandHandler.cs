using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.DeleteNote;

public sealed class DeleteNoteCommandHandler : IRequestHandler<DeleteNoteCommand, bool>
{
    private readonly INoteRepository _noteRepository;

    public DeleteNoteCommandHandler(INoteRepository noteRepository)
    {
        _noteRepository = noteRepository;
    }

    /// <summary>
    /// Returns false instead of throwing when the note is missing or not owned by the requesting user,
    /// so the API can turn that into a 404 either way, without leaking which is the case.
    /// </summary>
    public async Task<bool> HandleAsync(DeleteNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await _noteRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
        if (note is null)
        {
            return false;
        }

        await _noteRepository.DeleteAsync(request.UserId, request.Id, cancellationToken);
        return true;
    }
}
