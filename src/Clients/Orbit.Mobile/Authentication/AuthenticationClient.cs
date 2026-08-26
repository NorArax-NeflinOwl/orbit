using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Users;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Authentication;

/// <summary>
/// Signing in and out. Talks to the API without the access-token handler in front of it - there is no
/// token to attach yet on the way in, and on the way out the refresh token is the thing being revoked.
///
/// Signing in needs a connection, for the same reason every account operation does (see
/// <see cref="AccountClient"/>): only the server can say whether a password is right, and only it can
/// issue the tokens everything else depends on.
/// </summary>
public sealed class AuthenticationClient
{
    private readonly HttpClient _httpClient;
    private readonly INetworkStatus _networkStatus;
    private readonly SessionStore _sessionStore;

    public AuthenticationClient(HttpClient httpClient, INetworkStatus networkStatus, SessionStore sessionStore)
    {
        _httpClient = httpClient;
        _networkStatus = networkStatus;
        _sessionStore = sessionStore;
    }

    /// <summary>
    /// Refused and offline are told apart deliberately: saying "wrong password" when the server was
    /// simply unreachable sends someone off to reset a password that was fine.
    /// </summary>
    public async Task<AccountOperationResult> SignInAsync(
        string emailOrUserName, string password, CancellationToken cancellationToken = default)
    {
        if (!_networkStatus.IsOnline)
        {
            return AccountOperationResult.RequiresConnection;
        }

        var response = await _httpClient.PostAsJsonAsync(
            "api/auth/login", new LoginRequest(emailOrUserName, password), cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            return AccountOperationResult.Refused("Those details weren't recognised.");
        }

        response.EnsureSuccessStatusCode();

        if (await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken) is not { } authResponse)
        {
            return AccountOperationResult.Refused("Orbit signed you in but sent nothing back.");
        }

        await _sessionStore.SetAsync(UserSession.FromAuthResponse(authResponse));
        return AccountOperationResult.Applied;
    }

    /// <summary>
    /// Revokes the refresh token server-side, then clears the session locally. The local half happens
    /// even when the call fails: a user who taps sign out on a phone with no signal must still end up
    /// signed out on that phone.
    /// </summary>
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        if (await _sessionStore.GetAsync() is { } session)
        {
            try
            {
                await _httpClient.PostAsJsonAsync(
                    "api/auth/logout", new RefreshTokenRequest(session.RefreshToken), cancellationToken);
            }
            catch (HttpRequestException)
            {
                // Offline. The token stays valid server-side until it expires, which is the same
                // position as an app that was simply deleted.
            }
        }

        await _sessionStore.ClearAsync();
    }
}
