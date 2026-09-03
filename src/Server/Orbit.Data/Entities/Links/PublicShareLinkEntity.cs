namespace Orbit.Data.Entities;

/// <summary>See Orbit.Core.Sharing.PublicShareLink.</summary>
public sealed class PublicShareLinkEntity
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public Guid OwnerUserId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
