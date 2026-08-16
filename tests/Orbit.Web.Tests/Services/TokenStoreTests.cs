using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

public sealed class TokenStoreTests
{
    [Fact]
    public async Task GetTokenAsync_returns_null_when_no_token_was_stored()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());

        var token = await tokenStore.GetTokenAsync();

        Assert.Null(token);
    }

    [Fact]
    public async Task GetTokenAsync_returns_the_token_that_was_set()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());

        await tokenStore.SetTokenAsync("a-token");

        Assert.Equal("a-token", await tokenStore.GetTokenAsync());
    }

    [Fact]
    public async Task GetRefreshTokenAsync_returns_null_when_no_refresh_token_was_stored()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());

        var refreshToken = await tokenStore.GetRefreshTokenAsync();

        Assert.Null(refreshToken);
    }

    [Fact]
    public async Task SetTokensAsync_stores_both_the_access_and_the_refresh_token()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());

        await tokenStore.SetTokensAsync("a-token", "a-refresh-token");

        Assert.Equal("a-token", await tokenStore.GetTokenAsync());
        Assert.Equal("a-refresh-token", await tokenStore.GetRefreshTokenAsync());
    }

    [Fact]
    public async Task ClearTokenAsync_removes_a_previously_stored_token()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        await tokenStore.SetTokenAsync("a-token");

        await tokenStore.ClearTokenAsync();

        Assert.Null(await tokenStore.GetTokenAsync());
    }

    [Fact]
    public async Task ClearTokenAsync_also_removes_a_previously_stored_refresh_token()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        await tokenStore.SetTokensAsync("a-token", "a-refresh-token");

        await tokenStore.ClearTokenAsync();

        Assert.Null(await tokenStore.GetRefreshTokenAsync());
    }
}
