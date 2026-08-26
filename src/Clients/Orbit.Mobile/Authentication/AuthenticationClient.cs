using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Users;

namespace Orbit.Mobile.Authentication;

/// <summary>
/// Signing in and out. Talks to the API without the access-token handler in front of it - there is no
/// token to attach yet on the way in, and on the way out the refresh token is the thing being revoked.
/// </summary>
public sealed class AuthenticationClient
{
    private readonly HttpClient _httpClient;
    private readonly SessionStore _sessionStore;

    public AuthenticationClient(HttpClient httpClient, SessionStore sessionStore)
    {
        _httpClient = httpClient;
        _sessionStore = sessionStore;
    }

    /// <summary>
    /// True when the credentials were accepted and the session stored. False means they were refused -
    /// the only outcome the sign-in screen can do anything about. Anything else (no network, a server
    /// error) throws, because telling the user their password is wrong when the server was simply
    /// unreachable sends them off to reset a password that was fine.
    /// </summary>
    public async Task<bool> SignInAsync(string emailOrUserName, string password, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/auth/login", new LoginRequest(emailOrUserName, password), cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
        if (authResponse is null)
        {
            return false;
        }

        await _sessionStore.SetAsync(UserSession.FromAuthResponse(authResponse));
        return true;
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
