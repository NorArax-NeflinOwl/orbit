namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a refresh token, mapped separately from <see cref="Orbit.Core.Users.RefreshToken"/>
/// so schema changes don't force changes onto domain logic, and vice versa.
/// </summary>
public sealed class RefreshTokenEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
