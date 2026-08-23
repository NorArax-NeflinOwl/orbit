using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.ReleaseNoteLock;

/// <summary>
/// Releases the edit lock on noteId if userId currently holds it - a no-op otherwise (already released,
/// expired and taken by someone else, or never held). Sent by NoteEditor.razor on Save, Cancel, and best-effort
/// from DisposeAsync when navigating away, so the common case releases the lock immediately instead of
/// only via Note.LockExpiresAtUtc's expiry.
/// </summary>
public sealed record ReleaseNoteLockCommand(Guid UserId, Guid NoteId) : IRequest<bool>;
