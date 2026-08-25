using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Api.Tests.TestDoubles;
using Orbit.GoogleIntegration;
using Xunit;

namespace Orbit.Api.Tests.Users;

/// <summary>
/// Covers the real verifier rather than the stub the sign-in tests use: what it does with a string that
/// is not a usable token at all. /api/auth/google takes this straight from an anonymous request body, so
/// every shape of junk has to come back as "no identity" instead of escaping as a 500.
/// </summary>
public sealed class GoogleIdentityVerifierTests
{
    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("forged.token.here")]
    [InlineData("...")]
    [InlineData("eyJhbGciOiJub25lIn0.bm90LWpzb24.")]
    public async Task VerifyAsync_returns_no_identity_for_a_token_that_cannot_be_read(string idToken)
    {
        // "forged.token.here" is the case that used to get through: three dot-separated segments look
        // enough like a JWT to get past the library's shape check, then fail deeper down as a JSON parse
        // error rather than the InvalidJwtException the catch was originally written for.
        var verifier = CreateVerifier();

        var identity = await verifier.VerifyAsync(idToken, CancellationToken.None);

        Assert.Null(identity);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VerifyAsync_returns_no_identity_for_an_empty_token(string idToken)
    {
        var identity = await CreateVerifier().VerifyAsync(idToken, CancellationToken.None);

        Assert.Null(identity);
    }

    [Fact]
    public async Task VerifyAsync_returns_no_identity_when_google_sign_in_is_not_configured()
    {
        // Without a client id there is no audience to check a token against, so nothing can be trusted.
        var verifier = CreateVerifier(clientId: string.Empty);

        Assert.False(verifier.IsConfigured);
        Assert.Null(await verifier.VerifyAsync("anything", CancellationToken.None));
    }

    private static GoogleIdentityVerifier CreateVerifier(string clientId = "000000000000-example.apps.googleusercontent.com")
        => new(
            new TestOptionsMonitor<GoogleAuthSettings>(new GoogleAuthSettings { ClientId = clientId }),
            NullLogger<GoogleIdentityVerifier>.Instance);
}
