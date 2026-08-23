using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.ShareNote;

/// <summary>
/// OwnerUserId is really "the caller" - despite the name, it doesn't have to be the note's actual owner,
/// just someone with access to it (see ShareNoteCommandHandler). Returns null when noteId doesn't exist
/// or isn't accessible to them, when they aren't allowed to share it at the requested level, or when
/// recipientUserId is the note's owner - the same "not found" response either way, so a caller can't
/// distinguish "doesn't exist" from "exists but you can't share it" by probing ids.
/// </summary>
[ClientAction(ClientActionCategory.ShareElement)]
public sealed record ShareNoteCommand(Guid OwnerUserId, Guid NoteId, Guid RecipientUserId, ShareAccessLevel AccessLevel = ShareAccessLevel.ReadOnly)
    : IRequest<ShareOutcome?>;
