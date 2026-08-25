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
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid? LockedByUserId { get; set; }
    public string? LockedByUserName { get; set; }
    public DateTimeOffset? LockExpiresAtUtc { get; set; }
}
