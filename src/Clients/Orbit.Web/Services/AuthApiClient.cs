using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Orbit.Contracts.Users;
using Orbit.Core.Users.Login;
using Orbit.Core.Users.RegisterUser;

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
            // A 409 is a refusal whether or not a body came with it, so reading the reason must not be
            // able to turn one into an exception - an empty or unfamiliar body just leaves the more
            // common of the two.
            var reason = await ReadRejectionReasonAsync(response, cancellationToken);
            return reason == nameof(RegistrationRejection.UserNameTaken)
                ? AuthResult.UserNameAlreadyTaken()
                : AuthResult.EmailAlreadyTaken();
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
            // A 401 is a refusal whether or not a body came with it, so reading the reason must not be
            // able to turn one into an exception - an unfamiliar or missing reason falls back to saying
            // only that something was wrong, which is what this used to say in every case.
            return AuthResult.Refused(await ReadLoginRejectionAsync(response, cancellationToken) switch
            {
                nameof(LoginRejection.NoSuchAccount) => AuthOutcome.NoSuchAccount,
                nameof(LoginRejection.WrongPassword) => AuthOutcome.WrongPassword,
                nameof(LoginRejection.NoPasswordSet) => AuthOutcome.PasswordNotSet,
                _ => AuthOutcome.InvalidCredentials
            });
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

    private static async Task<string?> ReadRejectionReasonAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var conflict = await response.Content.ReadFromJsonAsync<RegistrationConflictDto>(cancellationToken: cancellationToken);
            return conflict?.Reason;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Mirrors ReadRejectionReasonAsync above, for the refusal a sign-in comes back with.</summary>
    private static async Task<string?> ReadLoginRejectionAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var rejection = await response.Content.ReadFromJsonAsync<LoginRejectionDto>(cancellationToken: cancellationToken);
            return rejection?.Reason;
        }
        catch (JsonException)
        {
            return null;
        }
    }

}
