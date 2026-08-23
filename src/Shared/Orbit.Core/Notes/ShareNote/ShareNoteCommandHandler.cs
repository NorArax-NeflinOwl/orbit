using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.ShareNote;

/// <summary>
/// Shares request.NoteId - either the caller's own note, or one shared with them - enforcing who is
/// allowed to re-share it and at what level:
/// <list type="bullet">
/// <item>The owner can share at any level, to anyone except themselves.</item>
/// <item>A recipient can re-share it only if their own access is <see cref="ShareAccessLevel.Share"/> or
/// <see cref="ShareAccessLevel.CanEdit"/> - never <see cref="ShareAccessLevel.ReadOnly"/> - and never at
/// a level higher than their own, so a re-share can never grant more access than the re-sharer holds.</item>
/// <item>Nobody, owner included, can share back to the note's owner (<see cref="Note.UserId"/>) - they
/// already have full access to their own note, so offering it back to them would be meaningless at best
/// and a way to bypass the level cap above at worst.</item>
/// </list>
/// A second offer to a recipient who already has one (accepted or still pending) doesn't create a
/// duplicate row - see <see cref="ShareOutcome.AlreadyShared"/>.
/// </summary>
public sealed class ShareNoteCommandHandler : IRequestHandler<ShareNoteCommand, ShareOutcome?>
{
    private readonly NoteAccessResolver _noteAccessResolver;
    private readonly INoteShareRepository _noteShareRepository;

    public ShareNoteCommandHandler(NoteAccessResolver noteAccessResolver, INoteShareRepository noteShareRepository)
    {
        _noteAccessResolver = noteAccessResolver;
        _noteShareRepository = noteShareRepository;
    }

    public async Task<ShareOutcome?> HandleAsync(ShareNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await _noteAccessResolver.ResolveAsync(request.OwnerUserId, request.NoteId, cancellationToken);
        if (note is null)
        {
            return null;
        }

        if (request.RecipientUserId == note.UserId)
        {
            return null;
        }

        if (note.IsShared && (note.AccessLevel < ShareAccessLevel.Share || request.AccessLevel > note.AccessLevel))
        {
            return null;
        }

        var existingShare = await _noteShareRepository.FindExistingAsync(note.Id, request.RecipientUserId, cancellationToken);
        if (existingShare is not null)
        {
            return new ShareOutcome(existingShare.Id, AlreadyShared: true);
        }

        var share = NoteShare.Create(note.Id, note.UserId, request.RecipientUserId, request.AccessLevel);
        await _noteShareRepository.AddAsync(share, cancellationToken);
        return new ShareOutcome(share.Id, AlreadyShared: false);
    }
}
