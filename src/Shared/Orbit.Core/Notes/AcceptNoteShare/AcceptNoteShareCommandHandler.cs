using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.AcceptNoteShare;

public sealed class AcceptNoteShareCommandHandler : IRequestHandler<AcceptNoteShareCommand, bool>
{
    private readonly INoteShareRepository _noteShareRepository;

    public AcceptNoteShareCommandHandler(INoteShareRepository noteShareRepository)
    {
        _noteShareRepository = noteShareRepository;
    }

    /// <summary>
    /// Marking the share accepted is the entire effect - unlike an earlier version of this feature, this
    /// no longer copies the note into the recipient's own notes, since sharing now grants access to the
    /// one true row instead (see NoteAccessResolver).
    /// </summary>
    public async Task<bool> HandleAsync(AcceptNoteShareCommand request, CancellationToken cancellationToken)
    {
        var share = await _noteShareRepository.GetByIdAsync(request.RecipientUserId, request.ShareId, cancellationToken);
        if (share is null)
        {
            return false;
        }

        if (!share.IsAccepted)
        {
            share.MarkAccepted();
            await _noteShareRepository.UpdateAsync(share, cancellationToken);
        }

        return true;
    }
}
