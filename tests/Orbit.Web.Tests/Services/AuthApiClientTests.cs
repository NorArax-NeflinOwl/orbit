using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Users;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

public sealed class AuthApiClientTests
{
    [Fact]
    public async Task LoginAsync_stores_the_token_and_reports_success_for_valid_credentials()
    {
        var authResponse = new AuthResponse("a-token", Guid.NewGuid(), "user@example.com", "User");
        var tokenStore = new TokenStore(new StubJSRuntime());
        var client = new AuthApiClient(CreateHttpClient(_ => JsonResponse(authResponse)), tokenStore);

        var result = await client.LoginAsync("user@example.com", "correct-password");

        Assert.Equal(AuthOutcome.Success, result.Outcome);
        Assert.Equal("a-token", await tokenStore.GetTokenAsync());
    }

    [Fact]
    public async Task LoginAsync_reports_invalid_credentials_without_storing_a_token()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        var client = new AuthApiClient(CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)), tokenStore);

        var result = await client.LoginAsync("user@example.com", "wrong-password");

        Assert.Equal(AuthOutcome.InvalidCredentials, result.Outcome);
        Assert.Null(await tokenStore.GetTokenAsync());
    }

    [Fact]
    public async Task RegisterAsync_stores_the_token_and_reports_success()
    {
        var authResponse = new AuthResponse("a-token", Guid.NewGuid(), "new@example.com", "New User");
        var tokenStore = new TokenStore(new StubJSRuntime());
        var client = new AuthApiClient(CreateHttpClient(_ => JsonResponse(authResponse)), tokenStore);

        var result = await client.RegisterAsync("new@example.com", "New User", "s3cret-password");

        Assert.Equal(AuthOutcome.Success, result.Outcome);
        Assert.Equal("a-token", await tokenStore.GetTokenAsync());
    }

    [Fact]
    public async Task RegisterAsync_reports_email_already_registered_without_storing_a_token()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        var client = new AuthApiClient(CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.Conflict)), tokenStore);

        var result = await client.RegisterAsync("taken@example.com", "Someone", "password");

        Assert.Equal(AuthOutcome.EmailAlreadyRegistered, result.Outcome);
        Assert.Null(await tokenStore.GetTokenAsync());
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> respond)
        => new(new StubHttpMessageHandler(respond)) { BaseAddress = new Uri("https://example.test/") };

    private static HttpResponseMessage JsonResponse(AuthResponse body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
}
