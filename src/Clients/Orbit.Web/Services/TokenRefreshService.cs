using System.Net.Http.Json;
using Orbit.Contracts.Users;

namespace Orbit.Web.Services;

/// <summary>
/// Exchanges the stored refresh token for a new access token. Shared by
/// <see cref="AuthorizationMessageHandler"/> (on-demand, when a live API call comes back 401) and
/// MainLayout's idle session heartbeat (proactive, so a tab left open past the access token's short
/// lifetime - see JwtSettings.ExpiryMinutes, 15 minutes today - doesn't look signed out in the UI while
/// the refresh token, good for 30 days, could have kept it alive). Uses a separate, unauthenticated
/// HttpClient - routing this call through AuthorizationMessageHandler itself would recurse into this
/// same retry logic.
/// </summary>
public sealed class TokenRefreshService
{
    private readonly TokenStore _tokenStore;
    private readonly HttpClient _refreshHttpClient;

    public TokenRefreshService(TokenStore tokenStore, HttpClient refreshHttpClient)
    {
        _tokenStore = tokenStore;
        _refreshHttpClient = refreshHttpClient;
    }

    /// <summary>
    /// True if a new access token was obtained and stored. False if there was no refresh token to use,
    /// or the server rejected it (expired or already revoked) - either way, both tokens are cleared and
    /// there is no way back into a signed-in state without the user logging in again.
    /// </summary>
    public async Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = await _tokenStore.GetRefreshTokenAsync();
        if (string.IsNullOrEmpty(refreshToken))
        {
            return false;
        }

        var response = await _refreshHttpClient.PostAsJsonAsync("api/auth/refresh", new RefreshTokenRequest(refreshToken), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await _tokenStore.ClearTokenAsync();
            return false;
        }

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken);
        if (authResponse is null)
        {
            return false;
        }

        await _tokenStore.SetTokensAsync(authResponse.Token, authResponse.RefreshToken);
        return true;
    }
}
