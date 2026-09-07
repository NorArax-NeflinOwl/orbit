using Orbit.Core.Abstractions;
using Orbit.Core.Folders;

namespace Orbit.Core.Notes.MoveNoteToFolder;

/// <summary>
/// Only the note's owner files it, and only into a folder of their own. Both checks matter: a folder id
/// arrives from a client, so one belonging to somebody else would otherwise file this note under a tab
/// its owner cannot see - it would simply vanish from every tab they have. Mirrors
/// SetNotePinnedCommandHandler on who may, and MoveTaskListToFolderCommandHandler on the rest.
/// </summary>
public sealed class MoveNoteToFolderCommandHandler : IRequestHandler<MoveNoteToFolderCommand, bool>
{
    private readonly INoteRepository _noteRepository;
    private readonly IFolderRepository _folderRepository;

    public MoveNoteToFolderCommandHandler(INoteRepository noteRepository, IFolderRepository folderRepository)
    {
        _noteRepository = noteRepository;
        _folderRepository = folderRepository;
    }

    public async Task<bool> HandleAsync(MoveNoteToFolderCommand request, CancellationToken cancellationToken)
    {
        var note = await _noteRepository.GetByIdAsync(request.UserId, request.NoteId, cancellationToken);
        if (note is null || note.UserId != request.UserId)
        {
            return false;
        }

        if (request.FolderId is { } folderId
            && await _folderRepository.GetByIdAsync(request.UserId, folderId, cancellationToken) is null)
        {
            return false;
        }

        note.MoveToFolder(request.FolderId);
        await _noteRepository.UpdateAsync(note, cancellationToken);
        return true;
    }
}
