using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes;

/// <summary>
/// A single note, owned by exactly one user (<see cref="UserId"/>) for its entire lifetime - sharing
/// (see NoteShare) grants other users access to this same row, it never creates a copy. Because of that,
/// <see cref="IsShared"/>/<see cref="SharedByUserName"/>/<see cref="AccessLevel"/> are not persisted at
/// all: they describe how the *current caller* relates to this note, recomputed fresh on every read by
/// NoteAccessResolver via <see cref="SetAccessContext"/> - the same underlying row reads differently for
/// its owner (IsShared false, AccessLevel CanEdit) than for someone it's been shared with.
/// </summary>
public sealed class Note
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    /// <summary>Empty for a private note - its real title is inside <see cref="EncryptedContent"/>.</summary>
    public string Title { get; private set; }

    /// <summary>Empty for a private note - its real lines are inside <see cref="EncryptedContent"/>.</summary>
    public IReadOnlyList<NoteContentLine> Content { get; private set; }

    /// <summary>
    /// Marks a note only its owner can ever read: its content is sealed in the browser before it gets
    /// here (see <see cref="EncryptedContent"/>), and it can't be shared - not "isn't shared yet", but
    /// refused by ShareNoteCommandHandler, with any existing shares revoked the moment it is turned on.
    /// </summary>
    public bool IsPrivate { get; private set; }

    /// <summary>
    /// Whether this note sits at the top of its owner's list. Only the owner's pin counts - see
    /// SetNotePinnedCommandHandler for why a recipient cannot pin a note shared with them.
    /// </summary>
    public bool IsPinned { get; private set; }

    /// <summary>The sealed title and lines of a private note; null for an ordinary one. See <see cref="EncryptedPayload"/>.</summary>
    public EncryptedPayload? EncryptedContent { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>The user id holding the current edit lock, if any - see AcquireLock/ReleaseLock.</summary>
    public Guid? LockedByUserId { get; private set; }

    /// <summary>The locking user's login, captured at lock-acquisition time for display - meaningless when LockedByUserId is null.</summary>
    public string? LockedByUserName { get; private set; }

    /// <summary>Once past, the lock is treated as abandoned (e.g. a crashed tab) and anyone can acquire a fresh one.</summary>
    public DateTimeOffset? LockExpiresAtUtc { get; private set; }

    /// <summary>False for the owner, true for anyone viewing/editing this note through a share - see NoteAccessResolver.</summary>
    public bool IsShared { get; private set; }

    /// <summary>The owner's login, whenever IsShared is true. Null otherwise.</summary>
    public string? SharedByUserName { get; private set; }

    /// <summary>The current caller's access level - always CanEdit for the owner, and whatever their share grants otherwise.</summary>
    public ShareAccessLevel AccessLevel { get; private set; } = ShareAccessLevel.CanEdit;

    private Note(
        Guid id, Guid userId, string title, IReadOnlyList<NoteContentLine> content, bool isPrivate, EncryptedPayload? encryptedContent,
        DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        Guid? lockedByUserId, string? lockedByUserName, DateTimeOffset? lockExpiresAtUtc, bool isPinned)
    {
        Id = id;
        UserId = userId;
        (Title, Content, IsPrivate, EncryptedContent) = ReadableOrSealed(title, content, isPrivate, encryptedContent);
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        LockedByUserId = lockedByUserId;
        LockedByUserName = lockedByUserName;
        LockExpiresAtUtc = lockExpiresAtUtc;
        IsPinned = isPinned;
    }

    public static Note Create(
        Guid userId, string title, IReadOnlyList<NoteContentLine> content, bool isPrivate = false,
        EncryptedPayload? encryptedContent = null, bool isPinned = false)
    {
        EnsureSealedWhenPrivate(isPrivate, encryptedContent);
        EnsureSomethingToRead(title, content, isPrivate);
        var now = DateTimeOffset.UtcNow;
        return new Note(
            Guid.NewGuid(), userId, title, content, isPrivate, encryptedContent, now, now,
            lockedByUserId: null, lockedByUserName: null, lockExpiresAtUtc: null, isPinned);
    }

    /// <summary>Rebuilds a note from already-persisted values, bypassing creation rules.</summary>
    public static Note FromPersistence(
        Guid id, Guid userId, string title, IReadOnlyList<NoteContentLine> content, bool isPrivate, EncryptedPayload? encryptedContent,
        DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        Guid? lockedByUserId, string? lockedByUserName, DateTimeOffset? lockExpiresAtUtc, bool isPinned = false)
        => new(id, userId, title, content, isPrivate, encryptedContent, createdAtUtc, updatedAtUtc,
            lockedByUserId, lockedByUserName, lockExpiresAtUtc, isPinned);

    /// <summary>
    /// Stamps how the current caller relates to this note - see the class comment. Called exactly once,
    /// by NoteAccessResolver, right after loading the row; never persisted.
    /// </summary>
    public void SetAccessContext(bool isShared, string? sharedByUserName, ShareAccessLevel accessLevel)
    {
        IsShared = isShared;
        SharedByUserName = sharedByUserName;
        AccessLevel = accessLevel;
    }

    /// <summary>
    /// Callers are expected to have already checked <see cref="AccessLevel"/> is CanEdit and that
    /// <see cref="IsLockedByAnotherUser"/> is false before calling this - see UpdateNoteCommandHandler.
    /// Kept out of this method itself so a locked/read-only note fails with a specific EditOutcome
    /// instead of a generic exception.
    /// </summary>
    public void Update(string title, IReadOnlyList<NoteContentLine> content, bool isPrivate, EncryptedPayload? encryptedContent)
    {
        EnsureSealedWhenPrivate(isPrivate, encryptedContent);
        EnsureSomethingToRead(title, content, isPrivate);
        (Title, Content, IsPrivate, EncryptedContent) = ReadableOrSealed(title, content, isPrivate, encryptedContent);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Keeps the two states from ever half-overlapping: a private note stores its sealed payload and
    /// nothing readable, an ordinary one stores readable content and no payload. Enforced here rather
    /// than trusted from callers, because a private note that still carried a readable title would break
    /// the only promise this feature makes.
    /// </summary>
    /// <summary>
    /// Refuses a note with nothing in it at all - no title and no line with anything on it. Such a note
    /// shows up in the list as a blank row that says nothing about itself and can only have been made by
    /// accident, so it is better not to have been made.
    ///
    /// A title on its own is enough: "Dentist on Tuesday" is a whole note, and demanding a body for it
    /// would be inventing a rule nobody asked for. A private note is exempt entirely - its readable
    /// fields travel empty by design, and what it actually says is inside the sealed payload where this
    /// cannot look.
    /// </summary>
    private static void EnsureSomethingToRead(string title, IReadOnlyList<NoteContentLine> content, bool isPrivate)
    {
        if (isPrivate)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(title) && !content.Any(line => !string.IsNullOrWhiteSpace(line.Text)))
        {
            throw new InvalidRequestException("A note needs a title or something written in it.");
        }
    }

    /// <summary>
    /// Refuses a private note arriving with nothing sealed inside it. Called from Create and Update
    /// only - never when rebuilding a stored row, which is deliberate: this rule exists to stop a bad
    /// write, and once such a row exists, throwing while reading it protects nothing and instead makes
    /// every good row alongside it unreachable. A stored row that says private but carries no payload
    /// therefore rebuilds as what it actually is - a private note nobody can open - which its owner
    /// can see and delete.
    /// </summary>
    private static void EnsureSealedWhenPrivate(bool isPrivate, EncryptedPayload? encryptedContent)
    {
        if (isPrivate && encryptedContent is null)
        {
            throw new InvalidRequestException("A private note must arrive already encrypted.");
        }
    }

    private static (string Title, IReadOnlyList<NoteContentLine> Content, bool IsPrivate, EncryptedPayload? EncryptedContent) ReadableOrSealed(
        string title, IReadOnlyList<NoteContentLine> content, bool isPrivate, EncryptedPayload? encryptedContent)
    {
        if (!isPrivate)
        {
            return (title, content, false, null);
        }

        // No check for a missing payload here - see EnsureSealedWhenPrivate for where that lives and why.
        return (string.Empty, [], true, encryptedContent);
    }

    /// <summary>
    /// Pinning is deliberately not part of Update: it moves a card on a page, it does not change what the
    /// note says, so it must not touch UpdatedAtUtc, take the edit lock, or need a body to send back.
    /// </summary>
    public void SetPinned(bool isPinned)
    {
        IsPinned = isPinned;
    }

    public bool IsLockedByAnotherUser(Guid callerId, DateTimeOffset nowUtc)
        => LockedByUserId is { } lockedByUserId && lockedByUserId != callerId && LockExpiresAtUtc > nowUtc;

    /// <summary>
    /// Grants userId the edit lock for lockDuration from nowUtc - safe to call again to refresh a lock
    /// this same user already holds (a heartbeat), and callers are expected to have already rejected the
    /// attempt via <see cref="IsLockedByAnotherUser"/> when someone else holds an unexpired one.
    /// </summary>
    public void AcquireLock(Guid userId, string userName, DateTimeOffset nowUtc, TimeSpan lockDuration)
    {
        LockedByUserId = userId;
        LockedByUserName = userName;
        LockExpiresAtUtc = nowUtc + lockDuration;
    }

    /// <summary>No-op if userId isn't the current lock holder, so releasing an already-expired-and-reassigned lock can't steal it back.</summary>
    public void ReleaseLock(Guid userId)
    {
        if (LockedByUserId != userId)
        {
            return;
        }

        LockedByUserId = null;
        LockedByUserName = null;
        LockExpiresAtUtc = null;
    }
}
