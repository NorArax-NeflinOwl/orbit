using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// Persists the current JWT in the browser's localStorage, so the user stays logged in across page
/// reloads. Blazor WebAssembly has no server-side session to rely on instead.
/// </summary>
public sealed class TokenStore
{
    private const string StorageKey = "orbit.authToken";

    private readonly IJSRuntime _jsRuntime;

    public TokenStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public ValueTask<string?> GetTokenAsync()
        => _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);

    public ValueTask SetTokenAsync(string token)
        => _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, token);

    public ValueTask ClearTokenAsync()
        => _jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
}
