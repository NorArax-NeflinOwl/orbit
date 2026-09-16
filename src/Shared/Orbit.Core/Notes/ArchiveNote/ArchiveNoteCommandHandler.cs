using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.ArchiveNote;

/// <summary>
/// Only the owner puts a note away, and only their own. A recipient must not: one row is one note, so
/// archiving one shared with them would take it off its owner's page - the same reason they cannot file
/// or pin one. Mirrors MoveNoteToFolderCommandHandler on who may.
/// </summary>
public sealed class ArchiveNoteCommandHandler : IRequestHandler<ArchiveNoteCommand, bool>
{
    private readonly INoteRepository _notes;

    public ArchiveNoteCommandHandler(INoteRepository notes) => _notes = notes;

    public async Task<bool> HandleAsync(ArchiveNoteCommand request, CancellationToken cancellationToken)
    {
        var found = await _notes.GetByIdAsync(request.UserId, request.NoteId, cancellationToken);
        if (found is null || found.UserId != request.UserId)
        {
            return false;
        }

        found.Archive(request.IsArchived);
        await _notes.UpdateAsync(found, cancellationToken);
        return true;
    }
}
