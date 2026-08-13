using System.Net;
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
        var handler = new AuthorizationMessageHandler(tokenStore) { InnerHandler = new RecordingHandler(request => capturedRequest = request) };
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };

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
        var handler = new AuthorizationMessageHandler(tokenStore) { InnerHandler = new RecordingHandler(request => capturedRequest = request) };
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };

        await httpClient.GetAsync("api/notes");

        Assert.Null(capturedRequest!.Headers.Authorization);
    }

    /// <summary>Terminal handler that records the request it receives and returns a canned empty response.</summary>
    private sealed class RecordingHandler(Action<HttpRequestMessage> onRequest) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            onRequest(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
