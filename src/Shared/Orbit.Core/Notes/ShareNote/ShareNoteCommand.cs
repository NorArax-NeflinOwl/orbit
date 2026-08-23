using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.ShareNote;

/// <summary>
/// Returns null when noteId doesn't exist or isn't accessible to ownerUserId, when ownerUserId isn't
/// allowed to share it (see ShareNoteCommandHandler), or when recipientUserId is the note's original
/// owner - the same "not found" response either way, so a caller can't distinguish "doesn't exist" from
/// "exists but you can't share it" by probing ids.
/// </summary>
[ClientAction(ClientActionCategory.ShareElement)]
public sealed record ShareNoteCommand(Guid OwnerUserId, Guid NoteId, Guid RecipientUserId, ShareAccessLevel AccessLevel = ShareAccessLevel.ReadOnly)
    : IRequest<ShareOutcome?>;
