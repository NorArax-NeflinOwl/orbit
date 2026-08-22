using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.ShareNote;

public sealed class ShareNoteCommandHandler : IRequestHandler<ShareNoteCommand, Guid?>
{
    private readonly INoteRepository _noteRepository;
    private readonly INoteShareRepository _noteShareRepository;

    public ShareNoteCommandHandler(INoteRepository noteRepository, INoteShareRepository noteShareRepository)
    {
        _noteRepository = noteRepository;
        _noteShareRepository = noteShareRepository;
    }

    public async Task<Guid?> HandleAsync(ShareNoteCommand request, CancellationToken cancellationToken)
    {
        var sourceNote = await _noteRepository.GetByIdAsync(request.OwnerUserId, request.NoteId, cancellationToken);
        if (sourceNote is null)
        {
            return null;
        }

        var share = NoteShare.Create(sourceNote.Id, request.OwnerUserId, request.RecipientUserId, request.AccessLevel);
        await _noteShareRepository.AddAsync(share, cancellationToken);
        return share.Id;
    }
}
