namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a task list, mapped separately from <see cref="Orbit.Core.Tasks.TaskList"/> so
/// schema changes don't force changes onto domain logic, and vice versa. Mirrors NoteEntity - see its
/// class comment for why there's no IsShared/SharedByUserName/AccessLevel here.
/// </summary>
public sealed class TaskEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>What it is about, under its name. Empty for one nobody described.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Whether this task list is readable only by its owner - see Orbit.Core's IsPrivate.</summary>
    public bool IsPrivate { get; set; }

    /// <summary>Base64 AES-GCM ciphertext of a private task list's title and content; null otherwise.</summary>
    public string? EncryptedCiphertext { get; set; }

    /// <summary>Base64 nonce the ciphertext above was sealed with; null otherwise.</summary>
    public string? EncryptedNonce { get; set; }
    public bool IsCompleted { get; set; }

    /// <summary>Whether this list gathers other lists - see Orbit.Core.Tasks.TaskList.IsGroup.</summary>
    public bool IsGroup { get; set; }

    /// <summary>The inventory this list's work is measured against, if any - see Orbit.Core.Tasks.TaskList.LinkedInventoryId.</summary>
    public Guid? LinkedInventoryId { get; set; }

    /// <summary>Stored by name, like every other enum here - see Orbit.Core.Abstractions.ItemPriority.</summary>
    public string Priority { get; set; } = nameof(Orbit.Core.Abstractions.ItemPriority.Normal);
    public bool IsPinned { get; set; }

    /// <summary>The user id currently holding the edit lock, if any - see Orbit.Core.Tasks.TaskList.LockedByUserId.</summary>
    public Guid? LockedByUserId { get; set; }

    /// <summary>The locking user's login, captured at lock-acquisition time. Null when LockedByUserId is null.</summary>
    public string? LockedByUserName { get; set; }

    /// <summary>Once past, the lock is treated as abandoned - see Orbit.Core.Tasks.TaskList.LockExpiresAtUtc.</summary>
    public DateTimeOffset? LockExpiresAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public List<TaskItemEntity> Items { get; set; } = [];
}
