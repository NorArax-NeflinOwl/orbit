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
    public async Task LoginAsync_stores_the_tokens_and_reports_success_for_a_valid_email()
    {
        var authResponse = new AuthResponse("a-token", "a-refresh-token", Guid.NewGuid(), "user@example.com", "User");
        var tokenStore = new TokenStore(new StubJSRuntime());
        var client = new AuthApiClient(CreateHttpClient(_ => JsonResponse(authResponse)), tokenStore);

        var result = await client.LoginAsync("user@example.com", "correct-password");

        Assert.Equal(AuthOutcome.Success, result.Outcome);
        Assert.Equal("a-token", await tokenStore.GetTokenAsync());
        Assert.Equal("a-refresh-token", await tokenStore.GetRefreshTokenAsync());
    }

    [Fact]
    public async Task LoginAsync_stores_the_tokens_and_reports_success_for_a_valid_username()
    {
        // AuthApiClient forwards whatever identifier it's given without inspecting it - the API
        // decides whether it looks like an email or a username - so this exercises the same code path
        // as the email test above with a username-shaped value instead.
        var authResponse = new AuthResponse("a-token", "a-refresh-token", Guid.NewGuid(), "user@example.com", "User");
        var tokenStore = new TokenStore(new StubJSRuntime());
        var client = new AuthApiClient(CreateHttpClient(_ => JsonResponse(authResponse)), tokenStore);

        var result = await client.LoginAsync("username", "correct-password");

        Assert.Equal(AuthOutcome.Success, result.Outcome);
        Assert.Equal("a-token", await tokenStore.GetTokenAsync());
        Assert.Equal("a-refresh-token", await tokenStore.GetRefreshTokenAsync());
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
    public async Task RegisterAsync_stores_the_tokens_and_reports_success()
    {
        var authResponse = new AuthResponse("a-token", "a-refresh-token", Guid.NewGuid(), "new@example.com", "New User");
        var tokenStore = new TokenStore(new StubJSRuntime());
        var client = new AuthApiClient(CreateHttpClient(_ => JsonResponse(authResponse)), tokenStore);

        var result = await client.RegisterAsync("new@example.com", "newuser", "New User", "s3cret-password");

        Assert.Equal(AuthOutcome.Success, result.Outcome);
        Assert.Equal("a-token", await tokenStore.GetTokenAsync());
        Assert.Equal("a-refresh-token", await tokenStore.GetRefreshTokenAsync());
    }

    [Theory]
    [InlineData("EmailTaken", AuthOutcome.EmailAlreadyTaken)]
    [InlineData("UserNameTaken", AuthOutcome.UserNameAlreadyTaken)]
    public async Task RegisterAsync_reports_which_field_was_taken_without_storing_a_token(
        string reason, AuthOutcome expected)
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        var client = new AuthApiClient(
            CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = JsonContent.Create(new RegistrationConflictDto(reason, "taken"))
            }),
            tokenStore);

        var result = await client.RegisterAsync("taken@example.com", "takenname", "Someone", "password");

        Assert.Equal(expected, result.Outcome);
        Assert.Null(await tokenStore.GetTokenAsync());
    }

    [Fact]
    public async Task RegisterAsync_falls_back_to_the_email_being_taken_when_the_reason_is_missing()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        var client = new AuthApiClient(CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.Conflict)), tokenStore);

        // A 409 with nothing to read is still a refusal; guessing the more common of the two beats
        // showing nothing, and either way no token is stored.
        var result = await client.RegisterAsync("taken@example.com", "takenname", "Someone", "password");

        Assert.Equal(AuthOutcome.EmailAlreadyTaken, result.Outcome);
        Assert.Null(await tokenStore.GetTokenAsync());
    }

    [Fact]
    public async Task LogoutAsync_revokes_the_refresh_token_on_the_api_and_clears_both_stored_tokens()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        await tokenStore.SetTokensAsync("a-token", "a-refresh-token");
        HttpRequestMessage? capturedRequest = null;
        var client = new AuthApiClient(
            CreateHttpClient(request =>
            {
                capturedRequest = request;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }),
            tokenStore);

        await client.LogoutAsync();

        Assert.Equal("api/auth/logout", capturedRequest!.RequestUri!.PathAndQuery.TrimStart('/'));
        Assert.Null(await tokenStore.GetTokenAsync());
        Assert.Null(await tokenStore.GetRefreshTokenAsync());
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> respond)
        => new(new StubHttpMessageHandler(respond)) { BaseAddress = new Uri("https://example.test/") };

    private static HttpResponseMessage JsonResponse(AuthResponse body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
}
