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
    public string Title { get; private set; }
    public IReadOnlyList<NoteContentLine> Content { get; private set; }
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
        Guid id, Guid userId, string title, IReadOnlyList<NoteContentLine> content, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        Guid? lockedByUserId, string? lockedByUserName, DateTimeOffset? lockExpiresAtUtc)
    {
        Id = id;
        UserId = userId;
        Title = title;
        Content = content;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        LockedByUserId = lockedByUserId;
        LockedByUserName = lockedByUserName;
        LockExpiresAtUtc = lockExpiresAtUtc;
    }

    public static Note Create(Guid userId, string title, IReadOnlyList<NoteContentLine> content)
    {
        var now = DateTimeOffset.UtcNow;
        return new Note(Guid.NewGuid(), userId, title, content, now, now, lockedByUserId: null, lockedByUserName: null, lockExpiresAtUtc: null);
    }

    /// <summary>Rebuilds a note from already-persisted values, bypassing creation rules.</summary>
    public static Note FromPersistence(
        Guid id, Guid userId, string title, IReadOnlyList<NoteContentLine> content, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        Guid? lockedByUserId, string? lockedByUserName, DateTimeOffset? lockExpiresAtUtc)
        => new(id, userId, title, content, createdAtUtc, updatedAtUtc, lockedByUserId, lockedByUserName, lockExpiresAtUtc);

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
    public void Update(string title, IReadOnlyList<NoteContentLine> content)
    {
        Title = title;
        Content = content;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
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
