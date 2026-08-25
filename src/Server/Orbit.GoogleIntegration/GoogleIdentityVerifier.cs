using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orbit.Core.Users;

namespace Orbit.GoogleIntegration;

/// <summary>
/// Verifies Google ID tokens with Google's own library, which checks the signature against Google's
/// published keys (fetching and caching them), the issuer, and the expiry. This class adds the two checks
/// that are specific to Orbit: that the token was issued for *this* deployment's client id, and that
/// Google itself considers the address verified.
/// </summary>
public sealed class GoogleIdentityVerifier : IGoogleIdentityVerifier
{
    private readonly IOptionsMonitor<GoogleAuthSettings> _settings;
    private readonly ILogger<GoogleIdentityVerifier> _logger;

    public GoogleIdentityVerifier(IOptionsMonitor<GoogleAuthSettings> settings, ILogger<GoogleIdentityVerifier> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.CurrentValue.ClientId);

    public async Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            // Audience is the crucial one: without it, a token minted for any other Google application
            // would validate here and let its holder sign in as that user.
            payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [_settings.CurrentValue.ClientId] });
        }
        catch (InvalidJwtException exception)
        {
            // Expected for anything an attacker sends, so logged as information rather than as an error.
            _logger.LogInformation(exception, "Rejected an invalid Google ID token");
            return null;
        }

        if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Email))
        {
            // Orbit treats a Google address as verified for password resets, so it has to actually be one
            // Google confirmed - an unverified one would let a reset go to a mailbox nobody proved they own.
            _logger.LogInformation("Rejected a Google ID token whose email address is not verified");
            return null;
        }

        var displayName = string.IsNullOrWhiteSpace(payload.Name) ? payload.Email : payload.Name;
        return new GoogleIdentity(payload.Subject, payload.Email.Trim().ToLowerInvariant(), displayName);
    }
}
