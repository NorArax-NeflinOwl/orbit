namespace Orbit.Data.Entities;

/// <summary>Persistence shape of a <see cref="Orbit.Core.Inventories.InventoryShare"/> - direct mirror of NoteShareEntity.</summary>
public sealed class InventoryShareEntity
{
    public Guid Id { get; set; }
    public Guid SourceInventoryId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid RecipientUserId { get; set; }

    /// <summary>Serialized <see cref="Orbit.Core.Abstractions.ShareAccessLevel"/> - "ReadOnly"/"Share"/"CanEdit".</summary>
    public string AccessLevel { get; set; } = "ReadOnly";

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
}
