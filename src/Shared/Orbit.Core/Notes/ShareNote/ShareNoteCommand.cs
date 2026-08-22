using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.ShareNote;

/// <summary>Returns null instead of a share id when noteId doesn't exist or isn't owned by ownerUserId.</summary>
[ClientAction(ClientActionCategory.ShareElement)]
public sealed record ShareNoteCommand(Guid OwnerUserId, Guid NoteId, Guid RecipientUserId, ShareAccessLevel AccessLevel = ShareAccessLevel.ReadOnly)
    : IRequest<Guid?>;
