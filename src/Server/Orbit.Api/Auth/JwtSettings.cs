namespace Orbit.Api.Auth;

/// <summary>
/// Configuration for issuing and validating JWTs, bound from the "Jwt" section of appsettings.json.
/// <see cref="SigningKey"/> is deliberately left out of appsettings.json - it's a secret, supplied only
/// through the JWT_SIGNING_KEY environment variable (see docker-compose.yml and .env.example) or, for
/// `dotnet run` outside Docker, dotnet user-secrets (see README.md). Access tokens are deliberately
/// short-lived - <see cref="RefreshTokenService"/> is what keeps a signed-in user from having to log in
/// again every <see cref="ExpiryMinutes"/> minutes.
/// </summary>
public sealed class JwtSettings
{
    public string Issuer { get; set; } = "Orbit.Api";
    public string Audience { get; set; } = "Orbit.Web";
    public int ExpiryMinutes { get; set; } = 15;
    public string SigningKey { get; set; } = string.Empty;
}
