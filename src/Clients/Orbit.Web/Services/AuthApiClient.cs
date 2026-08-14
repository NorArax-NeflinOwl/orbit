using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Users;

namespace Orbit.Web.Services;

/// <summary>
/// Thin wrapper around Orbit.Api's /api/auth endpoints. Stores the issued token on a successful login
/// or registration and clears it on logout, so callers only deal with pass/fail outcomes rather than
/// tokens directly.
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

        return await StoreTokenAndSucceedAsync(response, cancellationToken);
    }

    public async Task<AuthResult> LoginAsync(string emailOrUserName, string password, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/auth/login", new LoginRequest(emailOrUserName, password), cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return AuthResult.InvalidCredentials();
        }

        return await StoreTokenAndSucceedAsync(response, cancellationToken);
    }

    public Task LogoutAsync() => _tokenStore.ClearTokenAsync().AsTask();

    private async Task<AuthResult> StoreTokenAndSucceedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
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

        await _tokenStore.SetTokenAsync(authResponse.Token);
        return AuthResult.Success();
    }
}
