using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Users;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

public sealed class AuthorizationMessageHandlerTests
{
    [Fact]
    public async Task SendAsync_attaches_the_bearer_token_when_one_is_stored()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        await tokenStore.SetTokenAsync("a-token");
        HttpRequestMessage? capturedRequest = null;
        var httpClient = CreateHttpClient(tokenStore, request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await httpClient.GetAsync("api/notes");

        Assert.NotNull(capturedRequest!.Headers.Authorization);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal("a-token", capturedRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task SendAsync_sends_no_authorization_header_when_no_token_is_stored()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        HttpRequestMessage? capturedRequest = null;
        var httpClient = CreateHttpClient(tokenStore, request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await httpClient.GetAsync("api/notes");

        Assert.Null(capturedRequest!.Headers.Authorization);
    }

    [Fact]
    public async Task SendAsync_refreshes_the_access_token_and_retries_once_after_a_401()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        await tokenStore.SetTokensAsync("expired-token", "a-refresh-token");
        var attemptCount = 0;
        var httpClient = CreateHttpClient(
            tokenStore,
            _ => ++attemptCount == 1 ? new HttpResponseMessage(HttpStatusCode.Unauthorized) : new HttpResponseMessage(HttpStatusCode.OK),
            _ => JsonResponse(new AuthResponse("new-token", "new-refresh-token", Guid.NewGuid(), "user@example.com", "User")));

        var response = await httpClient.GetAsync("api/notes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, attemptCount);
        Assert.Equal("new-token", await tokenStore.GetTokenAsync());
        Assert.Equal("new-refresh-token", await tokenStore.GetRefreshTokenAsync());
    }

    [Fact]
    public async Task SendAsync_returns_the_original_401_without_retrying_when_the_refresh_token_is_also_rejected()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        await tokenStore.SetTokensAsync("expired-token", "a-stale-refresh-token");
        var attemptCount = 0;
        var httpClient = CreateHttpClient(
            tokenStore,
            _ =>
            {
                attemptCount++;
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            },
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var response = await httpClient.GetAsync("api/notes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, attemptCount);
        Assert.Null(await tokenStore.GetTokenAsync());
    }

    private static HttpClient CreateHttpClient(
        TokenStore tokenStore,
        Func<HttpRequestMessage, HttpResponseMessage> respondToInnerRequest,
        Func<HttpRequestMessage, HttpResponseMessage>? respondToRefreshRequest = null)
    {
        var refreshHttpClient = new HttpClient(
            new StubHttpMessageHandler(respondToRefreshRequest ?? (_ => new HttpResponseMessage(HttpStatusCode.Unauthorized))))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var tokenRefreshService = new TokenRefreshService(tokenStore, refreshHttpClient);
        var handler = new AuthorizationMessageHandler(
            tokenStore, tokenRefreshService, new OrbitAuthenticationStateProvider(tokenStore, tokenRefreshService))
        {
            InnerHandler = new StubHttpMessageHandler(respondToInnerRequest)
        };

        return new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
    }

    private static HttpResponseMessage JsonResponse(AuthResponse body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
}
