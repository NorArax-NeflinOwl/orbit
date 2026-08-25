using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory;

/// <summary>
/// A named container for inventory items, owned by exactly one user for its entire lifetime - sharing
/// (see WarehouseShare) grants other users access to this same row, it never creates a copy. Items no
/// longer carry an owner of their own: <see cref="InventoryItem.WarehouseId"/> points here, and access
/// to an item is whatever access the caller has to its warehouse.
///
/// <see cref="IsShared"/>/<see cref="SharedByUserName"/>/<see cref="AccessLevel"/> are not persisted:
/// they describe how the *current caller* relates to this warehouse, recomputed on every read by
/// WarehouseAccessResolver via <see cref="SetAccessContext"/> - exactly the shape Note uses.
///
/// Carries the same edit lock Note/TaskList/CalendarEvent do: the whole warehouse - its name and every
/// item in it - is edited in one form and saved in one go (see UpdateWarehouseCommand), so two people
/// editing at once would silently overwrite each other without it.
/// </summary>
public sealed class Warehouse
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>False for the owner, true for anyone reaching this warehouse through a share.</summary>
    public bool IsShared { get; private set; }

    /// <summary>The owner's login, whenever IsShared is true. Null otherwise.</summary>
    public string? SharedByUserName { get; private set; }

    /// <summary>The current caller's access level - always CanEdit for the owner, and whatever their share grants otherwise.</summary>
    public ShareAccessLevel AccessLevel { get; private set; } = ShareAccessLevel.CanEdit;

    /// <summary>The user id currently holding the edit lock, if any - see AcquireLock/ReleaseLock.</summary>
    public Guid? LockedByUserId { get; private set; }

    /// <summary>The locking user's login, captured at lock-acquisition time for display - meaningless when LockedByUserId is null.</summary>
    public string? LockedByUserName { get; private set; }

    /// <summary>Once past, the lock is treated as abandoned (e.g. a crashed tab) and anyone can acquire a fresh one.</summary>
    public DateTimeOffset? LockExpiresAtUtc { get; private set; }

    private Warehouse(
        Guid id, Guid userId, string name, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        Guid? lockedByUserId, string? lockedByUserName, DateTimeOffset? lockExpiresAtUtc)
    {
        Id = id;
        UserId = userId;
        Name = name;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        LockedByUserId = lockedByUserId;
        LockedByUserName = lockedByUserName;
        LockExpiresAtUtc = lockExpiresAtUtc;
    }

    public static Warehouse Create(Guid userId, string name)
    {
        var now = DateTimeOffset.UtcNow;
        return new Warehouse(Guid.NewGuid(), userId, name, now, now, lockedByUserId: null, lockedByUserName: null, lockExpiresAtUtc: null);
    }

    /// <summary>Rebuilds a warehouse from already-persisted values, bypassing creation rules.</summary>
    public static Warehouse FromPersistence(
        Guid id, Guid userId, string name, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        Guid? lockedByUserId, string? lockedByUserName, DateTimeOffset? lockExpiresAtUtc)
        => new(id, userId, name, createdAtUtc, updatedAtUtc, lockedByUserId, lockedByUserName, lockExpiresAtUtc);

    /// <summary>
    /// Stamps how the current caller relates to this warehouse - see the class comment. Called exactly
    /// once, by WarehouseAccessResolver, right after loading the row; never persisted.
    /// </summary>
    public void SetAccessContext(bool isShared, string? sharedByUserName, ShareAccessLevel accessLevel)
    {
        IsShared = isShared;
        SharedByUserName = sharedByUserName;
        AccessLevel = accessLevel;
    }

    /// <summary>
    /// Callers are expected to have already checked <see cref="AccessLevel"/> is CanEdit and that
    /// <see cref="IsLockedByAnotherUser"/> is false - see UpdateWarehouseCommandHandler. Kept out of this
    /// method so a locked/read-only warehouse fails with a specific EditOutcome instead of an exception.
    /// </summary>
    public void Update(string name)
    {
        Name = name;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public bool IsLockedByAnotherUser(Guid callerId, DateTimeOffset nowUtc)
        => LockedByUserId is { } lockedByUserId && lockedByUserId != callerId && LockExpiresAtUtc > nowUtc;

    /// <summary>Mirrors Note.AcquireLock - see its comment.</summary>
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
