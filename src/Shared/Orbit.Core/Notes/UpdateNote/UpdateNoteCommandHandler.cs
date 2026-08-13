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
    /// Returns false instead of throwing when the note is missing, so the API can turn that into a 404.
    /// </summary>
    public async Task<bool> HandleAsync(UpdateNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await _noteRepository.GetByIdAsync(request.Id, cancellationToken);
        if (note is null)
        {
            return false;
        }

        note.Update(request.Title, request.Content);
        await _noteRepository.UpdateAsync(note, cancellationToken);
        return true;
    }
}
