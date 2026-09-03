using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Orbit.Contracts.Users;

namespace Orbit.Mobile.Authentication;

/// <summary>
/// Exchanges the stored refresh token for a new access token. Uses its own unauthenticated
/// <see cref="HttpClient"/> - routing this through <see cref="AuthorizationMessageHandler"/> would
/// recurse straight back into the retry that called it.
///
/// <b>One of these per app.</b> The guard below is per instance, so a second instance is a second
/// redemption of a single-use token - which is a sign-out, not a slow path. The head registers it as a
/// singleton for that reason; AddHttpClient&lt;T&gt; on its own would make it transient.
/// </summary>
public sealed class TokenRefreshService
{
    private readonly SessionStore _sessionStore;
    private readonly HttpClient _refreshHttpClient;
    private readonly ILogger<TokenRefreshService> _logger;
    private readonly object _refreshLock = new();

    /// <summary>
    /// Refresh tokens are single-use: the server rotates and revokes the redeemed one atomically. When
    /// the access token expires with several requests in flight - which is exactly what active use looks
    /// like - each one refreshing independently would race to redeem the *same* token, and every loser
    /// of that race gets an already-used rejection that signs the user out mid-use. Orbit.Web shipped
    /// that bug and fixed it here; this client is built with the fix rather than rediscovering it.
    /// Handing every concurrent caller the one in-flight task makes a burst share a single redemption.
    /// </summary>
    private Task<bool>? _inFlightRefresh;

    public TokenRefreshService(SessionStore sessionStore, HttpClient refreshHttpClient, ILogger<TokenRefreshService> logger)
    {
        _sessionStore = sessionStore;
        _refreshHttpClient = refreshHttpClient;
        _logger = logger;
    }

    /// <summary>
    /// True when a new access token was obtained and stored. False when there was no session to refresh
    /// or the server rejected the token - in which case the session is cleared, and there is no way back
    /// to a signed-in state without logging in again.
    /// </summary>
    public Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default)
    {
        Task<bool> inFlight;
        lock (_refreshLock)
        {
            if (_inFlightRefresh is null || _inFlightRefresh.IsCompleted)
            {
                // Deliberately not the caller's token: the work is shared, so letting whoever happened to
                // arrive first cancel it would cancel it for everyone waiting behind them. Callers who
                // want to stop waiting do so below, without abandoning the redemption itself.
                _inFlightRefresh = RefreshCoreAsync();
            }

            inFlight = _inFlightRefresh;
        }

        return inFlight.WaitAsync(cancellationToken);
    }

    private async Task<bool> RefreshCoreAsync()
    {
        if (await _sessionStore.GetAsync() is not { } session)
        {
            return false;
        }

        var response = await _refreshHttpClient.PostAsJsonAsync(
            "api/auth/refresh", new RefreshTokenRequest(session.RefreshToken));

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogInformation("The refresh token was rejected ({StatusCode}); signing out", (int)response.StatusCode);
            await _sessionStore.ClearAsync();
            return false;
        }

        if (await response.Content.ReadFromJsonAsync<AuthResponse>() is not { } refreshed)
        {
            return false;
        }

        await _sessionStore.SetAsync(UserSession.FromAuthResponse(refreshed));
        return true;
    }
}
