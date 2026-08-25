using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Users;

namespace Orbit.Web.Services;

/// <summary>
/// Thin wrapper around Orbit.Api's /api/auth endpoints. Stores the issued access and refresh tokens on
/// a successful login or registration, and clears them (after telling the API to revoke the refresh
/// token server-side) on logout, so callers only deal with pass/fail outcomes rather than tokens
/// directly.
/// </summary>
public sealed class AuthApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenStore _tokenStore;

    public AuthApiClient(HttpClient httpClient, TokenStore tokenStore)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
    }

    public async Task<AuthResult> RegisterAsync(
        string email, string userName, string displayName, string password, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/auth/register", new RegisterUserRequest(email, userName, displayName, password), cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return AuthResult.EmailOrUserNameAlreadyTaken();
        }

        return await StoreTokensAndSucceedAsync(response, cancellationToken);
    }

    /// <summary>
    /// Signs in with a Google ID token, which registers the account on first use - the browser can't tell
    /// the two apart, and neither should the caller (see SignInWithGoogleCommandHandler).
    /// </summary>
    public async Task<AuthResult> SignInWithGoogleAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/auth/google", new GoogleSignInRequest(idToken), cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return AuthResult.InvalidCredentials();
        }

        return await StoreTokensAndSucceedAsync(response, cancellationToken);
    }

    /// <summary>Requests a password-reset code by email. Reports nothing about the account - see RequestPasswordResetCommand.</summary>
    public async Task RequestPasswordResetAsync(string emailOrUserName, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/auth/password-reset", new RequestPasswordResetRequest(emailOrUserName), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>False when the code is wrong, expired, already used, or out of attempts.</summary>
    public async Task<bool> ResetPasswordAsync(
        string emailOrUserName, string code, string newPassword, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/auth/password-reset/confirm", new ResetPasswordRequest(emailOrUserName, code, newPassword), cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<AuthResult> LoginAsync(string emailOrUserName, string password, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/auth/login", new LoginRequest(emailOrUserName, password), cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return AuthResult.InvalidCredentials();
        }

        return await StoreTokensAndSucceedAsync(response, cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = await _tokenStore.GetRefreshTokenAsync();
        if (!string.IsNullOrEmpty(refreshToken))
        {
            // Best-effort: the user is signed out locally either way below, even if this call fails
            // (e.g. the network is down), so its result is intentionally not checked.
            await _httpClient.PostAsJsonAsync("api/auth/logout", new RefreshTokenRequest(refreshToken), cancellationToken);
        }

        await _tokenStore.ClearTokenAsync();
    }

    private async Task<AuthResult> StoreTokensAndSucceedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return AuthResult.UnexpectedError();
        }

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken);
        if (authResponse is null)
        {
            return AuthResult.UnexpectedError();
        }

        await _tokenStore.SetTokensAsync(authResponse.Token, authResponse.RefreshToken);
        return AuthResult.Success();
    }
}
