namespace Orbit.Core.Users;

/// <summary>
/// The identity behind a Google ID token, once the token has been proved genuine.
/// </summary>
/// <param name="SubjectId">Google's stable, never-reassigned id for the account - what Orbit links on.</param>
/// <param name="Email">The address Google holds, already confirmed by Google itself.</param>
/// <param name="DisplayName">The person's name as Google reports it, used to seed a new account's display name.</param>
public sealed record GoogleIdentity(string SubjectId, string Email, string DisplayName);

/// <summary>
/// Validates a Google ID token: its signature against Google's published keys, its audience against this
/// deployment's client id, and its issuer and expiry. Implemented in Orbit.GoogleIntegration on top of
/// Google's own library rather than hand-rolled here - getting JWT validation subtly wrong is exactly the
/// kind of mistake that turns "sign in with Google" into "sign in as anyone".
/// </summary>
public interface IGoogleIdentityVerifier
{
    /// <summary>Whether this deployment has a Google client id configured at all - the sign-in button is hidden without one.</summary>
    bool IsConfigured { get; }

    /// <summary>Null when the token is missing, malformed, expired, meant for a different audience, or otherwise untrustworthy.</summary>
    Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken);
}
