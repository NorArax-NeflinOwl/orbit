using Orbit.Core.Abstractions;

using Orbit.Core.Notifications;

namespace Orbit.Core.Notes.ShareNote;

/// <summary>
/// Shares request.NoteId - either the caller's own note, or one shared with them - enforcing who is
/// allowed to re-share it and at what level:
/// <list type="bullet">
/// <item>The owner can share at any level, to anyone except themselves.</item>
/// <item>A recipient can re-share it only within what their own level permits - see
/// <see cref="ShareAccess.CanGrant"/>, which holds that rule for all four kinds of item at once.</item>
/// <item>Nobody, owner included, can share back to the note's owner (<see cref="Note.UserId"/>) - they
/// already have full access to their own note, so offering it back to them would be meaningless at best
/// and a way to bypass the level cap above at worst.</item>
/// </list>
/// A second offer to a recipient who already has one (accepted or still pending) doesn't create a
/// duplicate row - see <see cref="ShareOutcome.AlreadyShared"/> - but it does raise what that offer
/// grants if the new level is higher, which is how a request for edit access gets answered.
/// </summary>
public sealed class ShareNoteCommandHandler : IRequestHandler<ShareNoteCommand, ShareOutcome?>
{
    private readonly NoteAccessResolver _noteAccessResolver;
    private readonly INoteShareRepository _noteShareRepository;
    private readonly ISharedItemNotifier _sharedItemNotifier;

    public ShareNoteCommandHandler(NoteAccessResolver noteAccessResolver, INoteShareRepository noteShareRepository, ISharedItemNotifier sharedItemNotifier)
    {
        _noteAccessResolver = noteAccessResolver;
        _noteShareRepository = noteShareRepository;
        _sharedItemNotifier = sharedItemNotifier;
    }

    public async Task<ShareOutcome?> HandleAsync(ShareNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await _noteAccessResolver.ResolveAsync(request.OwnerUserId, request.NoteId, cancellationToken);
        if (note is null)
        {
            return null;
        }

        if (note.IsPrivate)
        {
            // A private note has no readable content on the server and is the owner's alone by
            // definition - refused here as well as hidden in the client, so a hand-made request can't
            // create a share that would only ever hand someone ciphertext they cannot open.
            throw new InvalidRequestException("A private note can't be shared.");
        }

        if (request.RecipientUserId == note.UserId)
        {
            return null;
        }

        if (note.IsShared && !note.AccessLevel.CanGrant(request.AccessLevel))
        {
            return null;
        }

        var existingShare = await _noteShareRepository.FindExistingAsync(note.Id, request.RecipientUserId, cancellationToken);
        if (existingShare is not null)
        {
            // Sharing again at a higher level raises the existing offer rather than being a no-op:
            // that is how an owner answers a request for edit access (see RequestEditAccess), and
            // "share it with them again, but with more" is what they mean by doing it.
            var accessLevelRaised = existingShare.RaiseAccessLevelTo(request.AccessLevel);
            if (accessLevelRaised)
            {
                await _noteShareRepository.UpdateAsync(existingShare, cancellationToken);
            }

            return new ShareOutcome(existingShare.Id, AlreadyShared: true, accessLevelRaised);
        }

        var share = NoteShare.Create(note.Id, note.UserId, request.RecipientUserId, request.AccessLevel);
        await _noteShareRepository.AddAsync(share, cancellationToken);
        await _sharedItemNotifier.NotifyAsync(
            request.RecipientUserId, request.OwnerUserId, SharedItemKind.Note, note.Title, cancellationToken);
        return new ShareOutcome(share.Id, AlreadyShared: false);
    }
}
