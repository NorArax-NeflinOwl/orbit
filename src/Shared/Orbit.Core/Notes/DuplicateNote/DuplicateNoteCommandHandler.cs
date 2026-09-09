using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.DuplicateNote;

public sealed class DuplicateNoteCommandHandler : IRequestHandler<DuplicateNoteCommand, Guid?>
{
    private readonly INoteRepository _noteRepository;

    public DuplicateNoteCommandHandler(INoteRepository noteRepository)
    {
        _noteRepository = noteRepository;
    }

    /// <summary>
    /// Everything the note says, in a note of its own. A sealed one is copied as it stands - the
    /// ciphertext is the note, and it opens with the same key, because the copy has the same owner. The
    /// pin is not copied: it says where a card sits on this reader's page, and two cards cannot both be
    /// the one being kept in front of them.
    /// </summary>
    public async Task<Guid?> HandleAsync(DuplicateNoteCommand request, CancellationToken cancellationToken)
    {
        if (await _noteRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken) is not { } note)
        {
            return null;
        }

        var copy = Note.Create(
            note.UserId,
            // A sealed note's title is inside the payload, so there is nothing here to rename - see
            // DuplicateRequest.Name.
            note.IsPrivate ? note.Title : request.Name ?? note.Title,
            note.Content,
            note.IsPrivate,
            note.EncryptedContent,
            isPinned: false,
            note.Priority,
            note.FolderId);
        await _noteRepository.AddAsync(copy, cancellationToken);
        return copy.Id;
    }
}
