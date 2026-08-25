namespace Orbit.Data.Entities;

/// <summary>Persistence shape of a <see cref="Orbit.Core.Inventory.WarehouseShare"/> - direct mirror of NoteShareEntity.</summary>
public sealed class WarehouseShareEntity
{
    public Guid Id { get; set; }
    public Guid SourceWarehouseId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid RecipientUserId { get; set; }

    /// <summary>Serialized <see cref="Orbit.Core.Abstractions.ShareAccessLevel"/> - "ReadOnly"/"Share"/"CanEdit".</summary>
    public string AccessLevel { get; set; } = "ReadOnly";

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
}
