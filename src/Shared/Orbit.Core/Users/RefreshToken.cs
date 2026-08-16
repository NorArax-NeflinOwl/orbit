namespace Orbit.Core.Users;

/// <summary>
/// An opaque, single-use credential that lets the client obtain a new access token without asking the
/// user to log in again. Stored hashed (see <see cref="IRefreshTokenRepository"/>), not in plaintext,
/// and rotated on every use: redeeming a token revokes it and issues a new one, so a leaked token that
/// gets replayed after the legitimate client already used it is detectable and rejected.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    private RefreshToken(
        Guid id, Guid userId, string tokenHash, DateTimeOffset expiresAtUtc, DateTimeOffset createdAtUtc, DateTimeOffset? revokedAtUtc)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = createdAtUtc;
        RevokedAtUtc = revokedAtUtc;
    }

    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTimeOffset.UtcNow;

    public static RefreshToken Create(Guid userId, string tokenHash, DateTimeOffset expiresAtUtc)
        => new(Guid.NewGuid(), userId, tokenHash, expiresAtUtc, DateTimeOffset.UtcNow, null);

    /// <summary>
    /// Rebuilds a refresh token from already-persisted values, bypassing creation rules.
    /// </summary>
    public static RefreshToken FromPersistence(
        Guid id, Guid userId, string tokenHash, DateTimeOffset expiresAtUtc, DateTimeOffset createdAtUtc, DateTimeOffset? revokedAtUtc)
        => new(id, userId, tokenHash, expiresAtUtc, createdAtUtc, revokedAtUtc);

    public void Revoke() => RevokedAtUtc = DateTimeOffset.UtcNow;
}
