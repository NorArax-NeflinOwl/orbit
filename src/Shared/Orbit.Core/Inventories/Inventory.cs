using Orbit.Core;
using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories;

/// <summary>
/// A named container for inventory items, owned by exactly one user for its entire lifetime - sharing
/// (see InventoryShare) grants other users access to this same row, it never creates a copy. Items no
/// longer carry an owner of their own: <see cref="InventoryItem.InventoryId"/> points here, and access
/// to an item is whatever access the caller has to its inventory.
///
/// <see cref="IsShared"/>/<see cref="SharedByUserName"/>/<see cref="AccessLevel"/> are not persisted:
/// they describe how the *current caller* relates to this inventory, recomputed on every read by
/// InventoryAccessResolver via <see cref="SetAccessContext"/> - exactly the shape Note uses.
///
/// Carries the same edit lock Note/TaskList/CalendarEvent do: the whole inventory - its name and every
/// item in it - is edited in one form and saved in one go (see UpdateInventoryCommand), so two people
/// editing at once would silently overwrite each other without it.
/// </summary>
public sealed class Inventory
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    /// <summary>Empty for a private inventory - its real name is inside <see cref="EncryptedContent"/>.</summary>
    public string Name { get; private set; }

    /// <summary>
    /// What this inventory is, under its name - the second and further lines of the one field the
    /// editor offers, the way a note is written. Empty for one nobody described.
    ///
    /// Not sealed with the rest when the inventory is private: it travels in the same encrypted
    /// payload as the name and the items do, so there is nothing readable left behind here either.
    /// </summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Marks an inventory only its owner can read. Its name and every item in it are sealed in the
    /// browser before they get here, so no item rows exist server-side at all - which is also why a
    /// private inventory raises no restock tasks and no expiry reminders: both are worked out from item
    /// rows the server no longer has.
    /// </summary>
    public bool IsPrivate { get; private set; }

    /// <summary>The sealed name and items of a private inventory; null for an ordinary one.</summary>
    public EncryptedPayload? EncryptedContent { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>False for the owner, true for anyone reaching this inventory through a share.</summary>
    public bool IsShared { get; private set; }

    /// <summary>The owner's login, whenever IsShared is true. Null otherwise.</summary>
    public string? SharedByUserName { get; private set; }

    /// <summary>
    /// True when somebody else holds accepted access to this. Only ever meaningful to the owner - the
    /// recipient's side of the same relationship is <see cref="IsShared"/>. Stamped by the access
    /// resolver rather than stored, because it depends on who is asking. See NoteDto for why a mobile
    /// client needs it.
    /// </summary>
    public bool IsSharedWithOthers { get; private set; }

    /// <summary>The current caller's access level - always CanEdit for the owner, and whatever their share grants otherwise.</summary>
    public ShareAccessLevel AccessLevel { get; private set; } = ShareAccessLevel.CanEdit;

    /// <summary>The user id currently holding the edit lock, if any - see AcquireLock/ReleaseLock.</summary>
    public Guid? LockedByUserId { get; private set; }

    /// <summary>The locking user's login, captured at lock-acquisition time for display - meaningless when LockedByUserId is null.</summary>
    public string? LockedByUserName { get; private set; }

    /// <summary>Once past, the lock is treated as abandoned (e.g. a crashed tab) and anyone can acquire a fresh one.</summary>
    public DateTimeOffset? LockExpiresAtUtc { get; private set; }

    private Inventory(
        Guid id, Guid userId, string name, bool isPrivate, EncryptedPayload? encryptedContent,
        DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        Guid? lockedByUserId, string? lockedByUserName, DateTimeOffset? lockExpiresAtUtc)
    {
        Id = id;
        UserId = userId;
        (Name, IsPrivate, EncryptedContent) = ReadableOrSealed(name, isPrivate, encryptedContent);
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        LockedByUserId = lockedByUserId;
        LockedByUserName = lockedByUserName;
        LockExpiresAtUtc = lockExpiresAtUtc;
    }

    public static Inventory Create(
        Guid userId, string name, bool isPrivate = false, EncryptedPayload? encryptedContent = null,
        string description = "")
    {
        StoredTextLimits.OrRefuse(name, StoredTextLimits.Title, "inventory's name");
        StoredTextLimits.OrRefuse(description, StoredTextLimits.EventDescription, "inventory's description");
        EnsureSealedWhenPrivate(isPrivate, encryptedContent);
        var now = DateTimeOffset.UtcNow;
        return Described(
            new Inventory(
                Guid.NewGuid(), userId, name, isPrivate, encryptedContent, now, now,
                lockedByUserId: null, lockedByUserName: null, lockExpiresAtUtc: null),
            description);
    }

    /// <summary>Rebuilds an inventory from already-persisted values, bypassing creation rules.</summary>
    public static Inventory FromPersistence(
        Guid id, Guid userId, string name, bool isPrivate, EncryptedPayload? encryptedContent,
        DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        Guid? lockedByUserId, string? lockedByUserName, DateTimeOffset? lockExpiresAtUtc,
        string description = "")
        => Described(
            new(id, userId, name, isPrivate, encryptedContent, createdAtUtc, updatedAtUtc,
                lockedByUserId, lockedByUserName, lockExpiresAtUtc),
            description);

    /// <summary>
    /// Puts a stored description back on a rebuilt inventory. A separate step because the constructor
    /// is shared with Create, which validates what it is given - and a row already in the database has
    /// been through that once already.
    /// </summary>
    private static Inventory Described(Inventory inventory, string description)
    {
        inventory.Description = description;
        return inventory;
    }

    /// <summary>
    /// Stamps how the current caller relates to this inventory - see the class comment. Called exactly
    /// once, by InventoryAccessResolver, right after loading the row; never persisted.
    /// </summary>
    /// <summary>Tells the owner that somebody else holds accepted access - the mirror of <see cref="IsShared"/>.</summary>
    public void SetSharedWithOthers(bool isSharedWithOthers) => IsSharedWithOthers = isSharedWithOthers;

    public void SetAccessContext(bool isShared, string? sharedByUserName, ShareAccessLevel accessLevel)
    {
        IsShared = isShared;
        SharedByUserName = sharedByUserName;
        AccessLevel = accessLevel;
    }

    /// <summary>
    /// Callers are expected to have already checked <see cref="AccessLevel"/> is CanEdit and that
    /// <see cref="IsLockedByAnotherUser"/> is false - see UpdateInventoryCommandHandler. Kept out of this
    /// method so a locked/read-only inventory fails with a specific EditOutcome instead of an exception.
    /// </summary>
    public void Update(string name, bool isPrivate, EncryptedPayload? encryptedContent, string description = "")
    {
        StoredTextLimits.OrRefuse(name, StoredTextLimits.Title, "inventory's name");
        StoredTextLimits.OrRefuse(description, StoredTextLimits.EventDescription, "inventory's description");
        Description = description;
        EnsureSealedWhenPrivate(isPrivate, encryptedContent);
        (Name, IsPrivate, EncryptedContent) = ReadableOrSealed(name, isPrivate, encryptedContent);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Mirrors Note.ReadableOrSealed: a private inventory stores its sealed payload and no readable
    /// name, an ordinary one the reverse. Enforced rather than trusted, because a private inventory that
    /// still carried a readable name would break the only promise this makes.
    /// </summary>
    /// <summary>Mirrors Note.EnsureSealedWhenPrivate - see its comment for why rebuilding a stored row deliberately skips this.</summary>
    private static void EnsureSealedWhenPrivate(bool isPrivate, EncryptedPayload? encryptedContent)
    {
        if (isPrivate && encryptedContent is null)
        {
            throw new InvalidRequestException("A private inventory must arrive already encrypted.");
        }
    }

    private static (string Name, bool IsPrivate, EncryptedPayload? EncryptedContent) ReadableOrSealed(
        string name, bool isPrivate, EncryptedPayload? encryptedContent)
    {
        if (!isPrivate)
        {
            return (name, false, null);
        }

        // No check for a missing payload here - see EnsureSealedWhenPrivate for where that lives and why.
        return (string.Empty, true, encryptedContent);
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
