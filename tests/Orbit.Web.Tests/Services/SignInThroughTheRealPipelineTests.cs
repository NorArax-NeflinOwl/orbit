using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Users;
using Orbit.Core.Users.Login;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Signing in through the pipeline the app actually assembles - AuthApiClient behind
/// AuthorizationMessageHandler, the way Program.cs wires it.
///
/// AuthApiClientTests exercises the client on its own, which is where the reason a refusal carries was
/// pinned; it passed while the real thing showed nothing, because the handler in front turns a 401 into
/// a refresh-and-retry. A sign-in's 401 is an answer about the password just typed, not a sign that a
/// session expired, and these tests are about that difference.
/// </summary>
public sealed class SignInThroughTheRealPipelineTests
{
    private static AuthApiClient CreateClient(
        TokenStore tokenStore,
        Func<HttpRequestMessage, HttpResponseMessage> respondToApi,
        Func<HttpRequestMessage, HttpResponseMessage>? respondToRefresh = null)
    {
        var refreshHttpClient = new HttpClient(
            new StubHttpMessageHandler(respondToRefresh ?? (_ => new HttpResponseMessage(HttpStatusCode.Unauthorized))))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var tokenRefreshService = new TokenRefreshService(tokenStore, refreshHttpClient);
        var handler = new AuthorizationMessageHandler(
            tokenStore, tokenRefreshService, new OrbitAuthenticationStateProvider(tokenStore, tokenRefreshService))
        {
            InnerHandler = new StubHttpMessageHandler(respondToApi)
        };

        return new AuthApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }, tokenStore);
    }

    private static HttpResponseMessage Refused(string reason)
        => new(HttpStatusCode.Unauthorized) { Content = JsonContent.Create(new LoginRejectionDto(reason, "ignored")) };

    [Fact]
    public async Task A_wrong_password_reaches_the_page_that_has_to_say_so()
    {
        var client = CreateClient(new TokenStore(new StubJSRuntime()), _ => Refused(nameof(LoginRejection.WrongPassword)));

        var result = await client.LoginAsync("anna@example.com", "wrong-password");

        Assert.Equal(AuthOutcome.WrongPassword, result.Outcome);
    }

    [Fact]
    public async Task A_stale_refresh_token_does_not_swallow_the_reason()
    {
        // The state somebody signing in is most likely to be in: a token left over from a previous
        // session. The handler used to spend it refreshing and retrying the sign-in, so what came back
        // was the answer to a second attempt rather than to the one that was made.
        var tokenStore = new TokenStore(new StubJSRuntime());
        await tokenStore.SetTokensAsync("stale-access-token", "stale-refresh-token");
        var attempts = 0;
        var client = CreateClient(
            tokenStore,
            _ =>
            {
                attempts++;
                return Refused(nameof(LoginRejection.WrongPassword));
            },
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new AuthResponse("new-access-token", "new-refresh-token", Guid.NewGuid(), "anna@example.com", "Anna"))
            });

        var result = await client.LoginAsync("anna@example.com", "wrong-password");

        Assert.Equal(AuthOutcome.WrongPassword, result.Outcome);
        // One attempt, not two: a refused sign-in must not quietly spend another go at the rate limit.
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task A_dead_leftover_token_does_not_hide_why_the_sign_in_was_refused()
    {
        // The state the reported bug happened in: a leftover refresh token that the server no longer
        // accepts. The handler used to spend it, fail, clear both tokens and announce the session had
        // ended - and the page showed nothing about the password at all.
        var tokenStore = new TokenStore(new StubJSRuntime());
        await tokenStore.SetTokensAsync("stale-access-token", "dead-refresh-token");
        var client = CreateClient(tokenStore, _ => Refused(nameof(LoginRejection.WrongPassword)));

        var result = await client.LoginAsync("anna@example.com", "wrong-password");

        Assert.Equal(AuthOutcome.WrongPassword, result.Outcome);
    }

    [Fact]
    public async Task A_refused_sign_in_leaves_the_stored_tokens_alone()
    {
        // Nothing about the session ended - somebody mistyped a password. Clearing the tokens here would
        // sign out a reader who was already signed in in another tab.
        var tokenStore = new TokenStore(new StubJSRuntime());
        await tokenStore.SetTokensAsync("access-token", "refresh-token");
        var client = CreateClient(tokenStore, _ => Refused(nameof(LoginRejection.NoSuchAccount)));

        await client.LoginAsync("nobody@example.com", "whatever");

        Assert.Equal("access-token", await tokenStore.GetTokenAsync());
    }
}
