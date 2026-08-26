using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orbit.Core.Users;

namespace Orbit.GoogleIntegration;

/// <summary>
/// Verifies Google ID tokens with Google's own library, which checks the signature against Google's
/// published keys (fetching and caching them), the issuer, and the expiry. This class adds the two checks
/// that are specific to Orbit: that the token was issued for one of *this* deployment's own clients (web
/// or either mobile app - see GoogleAuthSettings.AcceptedClientIds), and that Google itself considers the
/// address verified.
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

    public bool IsConfigured => _settings.CurrentValue.IsConfigured;

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
            // would validate here and let its holder sign in as that user. Widening it to several
            // audiences is safe only because it stays an explicit allowlist of this deployment's own
            // clients - see GoogleAuthSettings.AcceptedClientIds.
            payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = _settings.CurrentValue.AcceptedClientIds });
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not HttpRequestException)
        {
            // Expected for anything an attacker sends, so logged as information rather than as an error.
            // Deliberately wider than InvalidJwtException: that is only raised for a token Google's
            // library can read but won't trust, while a string it can't read at all (bad base64,
            // segments that aren't JSON) surfaces as a parse error from inside its JSON stack instead -
            // which used to escape as a 500 on an endpoint anyone can post junk to. HttpRequestException
            // is left to propagate on purpose: that is Google being unreachable, a real server-side
            // failure rather than a bad token, and answering "invalid token" would misdescribe it.
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
