using System.Security.Cryptography;
using System.Text;
using Orbit.Core.Users;

namespace Orbit.Api.Auth;

/// <summary>
/// Issues and redeems refresh tokens. Kept as a plain service rather than an Orbit.Core CQRS command,
/// mirroring <see cref="TokenService"/> and <see cref="PasswordHasher"/>: this is authentication
/// infrastructure, not application logic, and Orbit.Api.Tests already instantiates infrastructure
/// classes like those two directly, so nothing is lost in testability by keeping this one here too.
/// </summary>
public sealed class RefreshTokenService
{
    // Long enough that a returning user rarely has to log in again, short enough that a stolen refresh
    // token doesn't grant indefinite access.
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public RefreshTokenService(IRefreshTokenRepository refreshTokenRepository)
    {
        _refreshTokenRepository = refreshTokenRepository;
    }

    /// <summary>
    /// Creates and stores a new refresh token for a user, returning the raw (unhashed) value to send to
    /// the client. Only the SHA-256 hash of it is ever persisted, so a database leak alone can't be used
    /// to sign in as the user - the same way a leaked password hash alone can't be used to log in.
    /// </summary>
    public async Task<string> IssueAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rawToken = GenerateRawToken();
        var refreshToken = RefreshToken.Create(userId, Hash(rawToken), DateTimeOffset.UtcNow.Add(RefreshTokenLifetime));
        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        return rawToken;
    }

    /// <summary>
    /// Validates a raw refresh token and, if it's still active, rotates it: the redeemed token is
    /// revoked and a new one is issued in the same call. Rotation means a leaked token that gets
    /// replayed after the legitimate client already redeemed it fails here (it was already revoked) -
    /// the signal that the token was compromised.
    /// </summary>
    public async Task<RefreshTokenRedemption?> RedeemAsync(string rawToken, CancellationToken cancellationToken)
    {
        var refreshToken = await FindActiveTokenAsync(rawToken, cancellationToken);
        if (refreshToken is null)
        {
            return null;
        }

        refreshToken.Revoke();
        await _refreshTokenRepository.UpdateAsync(refreshToken, cancellationToken);

        var newRawToken = await IssueAsync(refreshToken.UserId, cancellationToken);
        return new RefreshTokenRedemption(refreshToken.UserId, newRawToken);
    }

    /// <summary>
    /// Revokes a refresh token without issuing a replacement, so it can no longer be redeemed. Used on
    /// logout. Does nothing (rather than failing) for a token that doesn't exist or is already inactive,
    /// since logout should always succeed from the client's point of view.
    /// </summary>
    public async Task RevokeAsync(string rawToken, CancellationToken cancellationToken)
    {
        var refreshToken = await FindActiveTokenAsync(rawToken, cancellationToken);
        if (refreshToken is null)
        {
            return;
        }

        refreshToken.Revoke();
        await _refreshTokenRepository.UpdateAsync(refreshToken, cancellationToken);
    }

    private async Task<RefreshToken?> FindActiveTokenAsync(string rawToken, CancellationToken cancellationToken)
    {
        var refreshToken = await _refreshTokenRepository.GetByTokenHashAsync(Hash(rawToken), cancellationToken);
        return refreshToken is not null && refreshToken.IsActive ? refreshToken : null;
    }

    private static string GenerateRawToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string rawToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}

/// <summary>Result of successfully redeeming a refresh token: who it belonged to, and its replacement.</summary>
public sealed record RefreshTokenRedemption(Guid UserId, string RefreshToken);
