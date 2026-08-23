using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.ShareNote;

/// <summary>
/// Shares request.NoteId - either the caller's own note, or a copy they received from someone else -
/// enforcing who is allowed to re-share a received copy and at what level:
/// <list type="bullet">
/// <item>The true owner (a note that isn't itself a shared copy) can share at any level, to anyone
/// except themselves.</item>
/// <item>A recipient can re-share their copy only if its <see cref="ShareAccessLevel"/> is
/// <see cref="ShareAccessLevel.Share"/> or <see cref="ShareAccessLevel.CanEdit"/> - never
/// <see cref="ShareAccessLevel.ReadOnly"/> - and never at a level higher than their own copy's, so a
/// re-share can never grant more access than the re-sharer holds.</item>
/// <item>Nobody, owner included, can share back to <see cref="Note.EffectiveOwnerUserId"/> - the person
/// who originally created the note already has it, so offering it back to them would be meaningless at
/// best and a way to bypass the level cap above at worst (accept a ReadOnly copy, "share" it back to the
/// real owner, has them accept a CanEdit copy of their own note - except the real owner can already
/// edit their own note, so this specific example is harmless, but the same trick would let a ReadOnly
/// holder launder a CanEdit copy to a third party by relaying it through the owner first if this weren't
/// blocked at every hop).</item>
/// </list>
/// A second offer to a recipient who already has one (accepted or still pending) doesn't create a
/// duplicate row - see <see cref="ShareOutcome.AlreadyShared"/>.
/// </summary>
public sealed class ShareNoteCommandHandler : IRequestHandler<ShareNoteCommand, ShareOutcome?>
{
    private readonly INoteRepository _noteRepository;
    private readonly INoteShareRepository _noteShareRepository;

    public ShareNoteCommandHandler(INoteRepository noteRepository, INoteShareRepository noteShareRepository)
    {
        _noteRepository = noteRepository;
        _noteShareRepository = noteShareRepository;
    }

    public async Task<ShareOutcome?> HandleAsync(ShareNoteCommand request, CancellationToken cancellationToken)
    {
        var sourceNote = await _noteRepository.GetByIdAsync(request.OwnerUserId, request.NoteId, cancellationToken);
        if (sourceNote is null)
        {
            return null;
        }

        var originalOwnerUserId = sourceNote.EffectiveOwnerUserId;
        if (request.RecipientUserId == originalOwnerUserId)
        {
            return null;
        }

        if (sourceNote.IsShared && (sourceNote.AccessLevel < ShareAccessLevel.Share || request.AccessLevel > sourceNote.AccessLevel))
        {
            return null;
        }

        var existingShare = await _noteShareRepository.FindExistingAsync(sourceNote.Id, request.RecipientUserId, cancellationToken);
        if (existingShare is not null)
        {
            return new ShareOutcome(existingShare.Id, AlreadyShared: true);
        }

        var share = NoteShare.Create(sourceNote.Id, request.OwnerUserId, request.RecipientUserId, originalOwnerUserId, request.AccessLevel);
        await _noteShareRepository.AddAsync(share, cancellationToken);
        return new ShareOutcome(share.Id, AlreadyShared: false);
    }
}
