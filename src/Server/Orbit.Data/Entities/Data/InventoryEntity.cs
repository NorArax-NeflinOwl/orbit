namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a <see cref="Orbit.Core.Inventories.Inventory"/> - the container inventory items
/// belong to. Mirrors NoteEntity, edit-lock columns included.
/// </summary>
public sealed class InventoryEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>What it is about, under its name. Empty for one nobody described.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Whether this inventory is readable only by its owner - see Orbit.Core.Inventories.Inventory.IsPrivate.</summary>
    public bool IsPrivate { get; set; }

    /// <summary>Base64 AES-GCM ciphertext of a private inventory's name and items; null otherwise.</summary>
    public string? EncryptedCiphertext { get; set; }

    /// <summary>Base64 nonce the ciphertext above was sealed with; null otherwise.</summary>
    public string? EncryptedNonce { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid? LockedByUserId { get; set; }
    public string? LockedByUserName { get; set; }
    public DateTimeOffset? LockExpiresAtUtc { get; set; }
}
