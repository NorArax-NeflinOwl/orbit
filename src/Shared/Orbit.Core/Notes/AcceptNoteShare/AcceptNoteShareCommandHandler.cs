using Orbit.Core.Abstractions;
using Orbit.Core.Users;

namespace Orbit.Core.Notes.AcceptNoteShare;

public sealed class AcceptNoteShareCommandHandler : IRequestHandler<AcceptNoteShareCommand, bool>
{
    private readonly INoteShareRepository _noteShareRepository;
    private readonly INoteRepository _noteRepository;
    private readonly IUserRepository _userRepository;

    public AcceptNoteShareCommandHandler(
        INoteShareRepository noteShareRepository, INoteRepository noteRepository, IUserRepository userRepository)
    {
        _noteShareRepository = noteShareRepository;
        _noteRepository = noteRepository;
        _userRepository = userRepository;
    }

    public async Task<bool> HandleAsync(AcceptNoteShareCommand request, CancellationToken cancellationToken)
    {
        var share = await _noteShareRepository.GetByIdAsync(request.RecipientUserId, request.ShareId, cancellationToken);
        if (share is null)
        {
            return false;
        }

        // Already accepted - report success without creating a second copy, so a duplicate click is harmless.
        if (share.IsAccepted)
        {
            return true;
        }

        var sourceNote = await _noteRepository.GetByIdAsync(share.OwnerUserId, share.SourceNoteId, cancellationToken);
        var owner = await _userRepository.GetByIdAsync(share.OwnerUserId, cancellationToken);
        if (sourceNote is null || owner is null)
        {
            return false;
        }

        var sharedNote = Note.CreateShared(
            request.RecipientUserId, sourceNote.Title, sourceNote.Content, owner.UserName, share.AccessLevel, share.OriginalOwnerUserId);
        await _noteRepository.AddAsync(sharedNote, cancellationToken);

        share.MarkAccepted(sharedNote.Id);
        await _noteShareRepository.UpdateAsync(share, cancellationToken);
        return true;
    }
}
