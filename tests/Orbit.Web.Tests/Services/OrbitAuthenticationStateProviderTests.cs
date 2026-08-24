using System.Net;
using System.Text;
using System.Text.Json;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

public sealed class OrbitAuthenticationStateProviderTests
{
    [Fact]
    public async Task GetAuthenticationStateAsync_returns_an_anonymous_principal_when_no_token_is_stored()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        var provider = new OrbitAuthenticationStateProvider(tokenStore, CreateTokenRefreshService(tokenStore));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_builds_a_principal_from_the_stored_token_claims()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        await tokenStore.SetTokenAsync(CreateUnsignedJwt(new Dictionary<string, string>
        {
            ["sub"] = "11111111-1111-1111-1111-111111111111",
            ["email"] = "user@example.com",
            ["name"] = "Test User"
        }));
        var provider = new OrbitAuthenticationStateProvider(tokenStore, CreateTokenRefreshService(tokenStore));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("Test User", state.User.Identity!.Name);
        Assert.Equal("user@example.com", state.User.FindFirst("email")?.Value);
        Assert.Equal("11111111-1111-1111-1111-111111111111", state.User.FindFirst("sub")?.Value);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_returns_an_anonymous_principal_when_the_stored_token_is_expired()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        await tokenStore.SetTokenAsync(CreateUnsignedJwt(new Dictionary<string, string>
        {
            ["sub"] = "11111111-1111-1111-1111-111111111111",
            // Any point well in the past - the exact value doesn't matter, only that it's expired.
            ["exp"] = "1000000000"
        }));
        var provider = new OrbitAuthenticationStateProvider(tokenStore, CreateTokenRefreshService(tokenStore));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    private static TokenRefreshService CreateTokenRefreshService(TokenStore tokenStore)
    {
        var refreshHttpClient = new HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        return new TokenRefreshService(tokenStore, refreshHttpClient);
    }

    /// <summary>
    /// Builds a JWT with a real header and payload but a dummy signature - enough to exercise the
    /// client's own claim-parsing logic, which never checks the signature (the server already did, on
    /// every API call that carries this token).
    /// </summary>
    private static string CreateUnsignedJwt(Dictionary<string, string> claims)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(claims));
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
