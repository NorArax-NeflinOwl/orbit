using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// Persists the current access and refresh tokens in the browser's localStorage, so the user stays
/// logged in across page reloads. Blazor WebAssembly has no server-side session to rely on instead.
/// </summary>
public sealed class TokenStore
{
    private const string AccessTokenStorageKey = "orbit.authToken";
    private const string RefreshTokenStorageKey = "orbit.refreshToken";

    private readonly IJSRuntime _jsRuntime;

    public TokenStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public ValueTask<string?> GetTokenAsync()
        => _jsRuntime.InvokeAsync<string?>("localStorage.getItem", AccessTokenStorageKey);

    public ValueTask<string?> GetRefreshTokenAsync()
        => _jsRuntime.InvokeAsync<string?>("localStorage.getItem", RefreshTokenStorageKey);

    public ValueTask SetTokenAsync(string token)
        => _jsRuntime.InvokeVoidAsync("localStorage.setItem", AccessTokenStorageKey, token);

    /// <summary>
    /// Stores the access token together with the refresh token issued alongside it - by login,
    /// registration, and by a successful token refresh.
    /// </summary>
    public async Task SetTokensAsync(string accessToken, string refreshToken)
    {
        await SetTokenAsync(accessToken);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", RefreshTokenStorageKey, refreshToken);
    }

    public async Task ClearTokenAsync()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AccessTokenStorageKey);
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", RefreshTokenStorageKey);
    }
}
