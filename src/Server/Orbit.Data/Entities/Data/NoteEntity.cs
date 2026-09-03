namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a note, mapped separately from <see cref="Orbit.Core.Notes.Note"/> so schema
/// changes don't force changes onto domain logic, and vice versa. UserId is always the note's one true,
/// permanent owner - sharing (see NoteShareEntity) grants other users access to this same row rather
/// than copying it, so unlike an earlier version of this table there is no IsShared/SharedByUserName/
/// AccessLevel here: those describe how a given caller relates to the note, computed fresh on every
/// read by NoteAccessResolver, never stored.
/// </summary>
public sealed class NoteEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>Whether the owner has pinned this note to the top of their list - see Orbit.Core.Notes.Note.IsPinned.</summary>
    public bool IsPinned { get; set; }

    /// <summary>Stored by name, like every other enum here - see Orbit.Core.Abstractions.ItemPriority.</summary>
    public string Priority { get; set; } = nameof(Orbit.Core.Abstractions.ItemPriority.Normal);

    /// <summary>Whether this note is readable only by its owner - see Orbit.Core's IsPrivate.</summary>
    public bool IsPrivate { get; set; }

    /// <summary>Base64 AES-GCM ciphertext of a private note's title and content; null otherwise.</summary>
    public string? EncryptedCiphertext { get; set; }

    /// <summary>Base64 nonce the ciphertext above was sealed with; null otherwise.</summary>
    public string? EncryptedNonce { get; set; }

    /// <summary>JSON-encoded list of NoteContentLine (text + checklist state per line) - SQLite has no native array/object column type. See CalendarEventEntity.GuestsJson for the same convention.</summary>
    public string ContentJson { get; set; } = "[]";

    /// <summary>The user id currently holding the edit lock, if any - see Orbit.Core.Notes.Note.LockedByUserId.</summary>
    public Guid? LockedByUserId { get; set; }

    /// <summary>The locking user's login, captured at lock-acquisition time. Null when LockedByUserId is null.</summary>
    public string? LockedByUserName { get; set; }

    /// <summary>Once past, the lock is treated as abandoned - see Orbit.Core.Notes.Note.LockExpiresAtUtc.</summary>
    public DateTimeOffset? LockExpiresAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
