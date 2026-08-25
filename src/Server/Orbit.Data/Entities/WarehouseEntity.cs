namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a <see cref="Orbit.Core.Inventory.Warehouse"/> - the container inventory items
/// belong to. Mirrors NoteEntity minus the edit-lock columns, which warehouses don't have.
/// </summary>
public sealed class WarehouseEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
