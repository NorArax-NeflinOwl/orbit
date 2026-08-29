using System.Net;
using System.Web;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Authentication;

/// <summary>
/// The half of signing in with Google that can be checked without a device. What is being guarded is
/// the flow's shape: this is a public client with no secret, so PKCE is the only thing binding the code
/// to the app that asked for it - an authorization request that lost its challenge would still work,
/// and would still be worth nothing.
/// </summary>
public sealed class GoogleSignInTests
{
    private const string ClientId = "181624005200-example.apps.googleusercontent.com";

    [Fact]
    public void The_authorization_address_carries_a_pkce_challenge_and_asks_for_a_code()
    {
        var query = QueryOf(BuildAddress());

        Assert.Equal(ClientId, query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrWhiteSpace(query["code_challenge"]));
    }

    /// <summary>openid is what makes Google answer with an ID token, which is the only thing Orbit wants.</summary>
    [Fact]
    public void The_authorization_address_asks_for_an_id_token()
        => Assert.Contains("openid", QueryOf(BuildAddress())["scope"]);

    /// <summary>
    /// The challenge is a hash of the verifier, never the verifier itself: the authorization request
    /// travels through a browser and its address bar, where the secret half must not appear.
    /// </summary>
    [Fact]
    public void The_verifier_itself_never_leaves_the_app()
    {
        var browser = new FakeSignInBrowser();
        var signIn = new GoogleSignIn(browser, StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK).ToHttpClient());

        var address = signIn.BuildAuthorizationAddress(ClientId, "a-known-verifier").ToString();

        Assert.DoesNotContain("a-known-verifier", address);
    }

    [Fact]
    public void The_address_sends_google_back_to_where_this_app_is_listening()
        => Assert.Equal(
            FakeSignInBrowser.Address.ToString(), QueryOf(BuildAddress())["redirect_uri"]);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Nothing_is_attempted_without_a_client_id(string clientId)
    {
        var browser = new FakeSignInBrowser();
        var signIn = new GoogleSignIn(browser, StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK).ToHttpClient());

        Assert.Null(await signIn.GetIdTokenAsync(clientId));
        Assert.False(browser.WasOpened);
    }

    [Fact]
    public async Task Backing_out_of_the_browser_is_not_a_failure()
    {
        var signIn = new GoogleSignIn(
            new FakeSignInBrowser { Result = null },
            StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK).ToHttpClient());

        Assert.Null(await signIn.GetIdTokenAsync(ClientId));
    }

    /// <summary>A refused consent screen comes back as an error rather than a code, and reads the same way.</summary>
    [Fact]
    public async Task A_callback_without_a_code_yields_no_token()
    {
        var signIn = new GoogleSignIn(
            new FakeSignInBrowser { Result = new Dictionary<string, string> { ["error"] = "access_denied" } },
            StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK).ToHttpClient());

        Assert.Null(await signIn.GetIdTokenAsync(ClientId));
    }

    [Fact]
    public async Task The_code_is_exchanged_for_the_id_token_google_returns()
    {
        var google = StubHttpMessageHandler.RespondingWith(new { id_token = "the-id-token" });
        var signIn = new GoogleSignIn(new FakeSignInBrowser(), google.ToHttpClient());

        Assert.Equal("the-id-token", await signIn.GetIdTokenAsync(ClientId));
    }

    [Fact]
    public async Task An_exchange_google_refuses_yields_no_token()
    {
        var signIn = new GoogleSignIn(
            new FakeSignInBrowser(),
            StubHttpMessageHandler.RespondingWith(HttpStatusCode.BadRequest).ToHttpClient());

        Assert.Null(await signIn.GetIdTokenAsync(ClientId));
    }

    private static Uri BuildAddress()
        => new GoogleSignIn(
                new FakeSignInBrowser(),
                StubHttpMessageHandler.RespondingWith(HttpStatusCode.OK).ToHttpClient())
            .BuildAuthorizationAddress(ClientId, "verifier");

    private static System.Collections.Specialized.NameValueCollection QueryOf(Uri address)
        => HttpUtility.ParseQueryString(address.Query);
}
