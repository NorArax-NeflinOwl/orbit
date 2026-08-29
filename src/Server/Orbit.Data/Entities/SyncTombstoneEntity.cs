namespace Orbit.Data.Entities;

/// <summary>See Orbit.Core.Sync.SyncTombstone.</summary>
public sealed class SyncTombstoneEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public DateTimeOffset DeletedAtUtc { get; set; }
}
