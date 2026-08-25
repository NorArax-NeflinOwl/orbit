namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a <see cref="Orbit.Core.Inventory.Warehouse"/> - the container inventory items
/// belong to. Mirrors NoteEntity, edit-lock columns included.
/// </summary>
public sealed class WarehouseEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether this warehouse is readable only by its owner - see Orbit.Core.Inventory.Warehouse.IsPrivate.</summary>
    public bool IsPrivate { get; set; }

    /// <summary>Base64 AES-GCM ciphertext of a private warehouse's name and items; null otherwise.</summary>
    public string? EncryptedCiphertext { get; set; }

    /// <summary>Base64 nonce the ciphertext above was sealed with; null otherwise.</summary>
    public string? EncryptedNonce { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid? LockedByUserId { get; set; }
    public string? LockedByUserName { get; set; }
    public DateTimeOffset? LockExpiresAtUtc { get; set; }
}
