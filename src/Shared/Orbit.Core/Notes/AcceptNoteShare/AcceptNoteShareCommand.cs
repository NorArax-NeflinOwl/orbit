using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.AcceptNoteShare;

/// <summary>Returns false when shareId doesn't exist, wasn't offered to recipientUserId, or its source note is gone.</summary>
public sealed record AcceptNoteShareCommand(Guid RecipientUserId, Guid ShareId) : IRequest<bool>;
