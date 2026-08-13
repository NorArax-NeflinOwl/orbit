using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.UpdateNote;

public sealed class UpdateNoteCommandHandler : IRequestHandler<UpdateNoteCommand, bool>
{
    private readonly INoteRepository _noteRepository;

    public UpdateNoteCommandHandler(INoteRepository noteRepository)
    {
        _noteRepository = noteRepository;
    }

    /// <summary>
    /// Returns false instead of throwing when the note is missing or not owned by the requesting user,
    /// so the API can turn that into a 404 either way, without leaking which is the case.
    /// </summary>
    public async Task<bool> HandleAsync(UpdateNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await _noteRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
        if (note is null)
        {
            return false;
        }

        note.Update(request.Title, request.Content);
        await _noteRepository.UpdateAsync(note, cancellationToken);
        return true;
    }
}
