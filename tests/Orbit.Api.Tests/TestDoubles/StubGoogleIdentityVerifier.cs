using Orbit.Core.Users;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// Stands in for Google's token validation so the account-linking rules can be tested without a network
/// call or a real signed token. Whether a token is genuine is deliberately not what these tests are
/// about - that lives in GoogleIdentityVerifier, on top of Google's own library.
/// </summary>
internal sealed class StubGoogleIdentityVerifier : IGoogleIdentityVerifier
{
    /// <summary>Any token other than this one is treated as untrustworthy, standing in for a forged or expired one.</summary>
    public const string ValidToken = "valid-google-token";

    private readonly GoogleIdentity _identity;

    public StubGoogleIdentityVerifier(string subjectId = "google-subject-1", string email = "alice@example.com", string displayName = "Alice")
    {
        _identity = new GoogleIdentity(subjectId, email, displayName);
    }

    public bool IsConfigured => true;

    public Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken)
        => Task.FromResult(idToken == ValidToken ? _identity : null);
}
