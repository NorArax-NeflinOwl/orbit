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
    private readonly object _refreshLock = new();

    /// <summary>
    /// Refresh tokens are single-use (the server rotates and revokes the redeemed one atomically - see
    /// RefreshTokenService.RedeemAsync) - if the access token expires while several requests are in
    /// flight at once (very plausible during active use, since that's exactly when a burst of API calls
    /// happens), each one independently calling TryRefreshAsync would race to redeem the *same* refresh
    /// token: only the first succeeds, and every other caller's redeem attempt is then rejected as
    /// already-used, which used to clear the tokens ClearTokenAsync clears - forcibly logging out a user
    /// who was actively using the app, not idle. Caching the in-flight task here and handing every
    /// concurrent caller the same one (instead of each starting its own redeem) makes a burst of callers
    /// share a single refresh instead of racing each other.
    /// </summary>
    private Task<bool>? _inFlightRefresh;

    public TokenRefreshService(TokenStore tokenStore, HttpClient refreshHttpClient)
    {
        _tokenStore = tokenStore;
        _refreshHttpClient = refreshHttpClient;
    }

    /// <summary>
    /// True if a new access token was obtained and stored. False if there was no refresh token to use,
    /// or the server rejected it (expired or already revoked) - either way, both tokens are cleared and
    /// there is no way back into a signed-in state without the user logging in again. Concurrent callers
    /// while a refresh is already in flight join that same attempt rather than starting their own - see
    /// <see cref="_inFlightRefresh"/>.
    /// </summary>
    public Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default)
    {
        lock (_refreshLock)
        {
            if (_inFlightRefresh is null || _inFlightRefresh.IsCompleted)
            {
                _inFlightRefresh = RefreshCoreAsync(cancellationToken);
            }

            return _inFlightRefresh;
        }
    }

    private async Task<bool> RefreshCoreAsync(CancellationToken cancellationToken)
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
