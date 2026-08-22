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
    /// Returns false instead of throwing when the note is missing, not owned by the requesting user, or
    /// is a read-only shared copy, so the API can turn any of those into a 404, without leaking which
    /// one applies.
    /// </summary>
    public async Task<bool> HandleAsync(UpdateNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await _noteRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken);
        if (note is null || (note.IsShared && note.AccessLevel == ShareAccessLevel.ReadOnly))
        {
            return false;
        }

        note.Update(request.Title, request.Content);
        await _noteRepository.UpdateAsync(note, cancellationToken);
        return true;
    }
}
