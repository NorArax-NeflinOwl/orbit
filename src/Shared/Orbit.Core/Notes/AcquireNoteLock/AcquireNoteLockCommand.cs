using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.AcquireNoteLock;

/// <summary>
/// Acquires (or refreshes, if userId already holds it) the edit lock on noteId - see
/// AcquireNoteLockCommandHandler. NoteEditor.razor sends this once when opening an editable note, then
/// again on a heartbeat while the editor stays open, so an abandoned lock (a crashed tab, a lost network
/// connection) expires on its own instead of blocking the note forever - see Note.LockExpiresAtUtc.
/// </summary>
public sealed record AcquireNoteLockCommand(Guid UserId, Guid NoteId) : IRequest<EditOutcome>;
