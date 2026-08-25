using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Users;

namespace Orbit.Web.Services;

/// <summary>
/// Thin wrapper around Orbit.Api's /api/users endpoints, keeping HTTP and JSON details out of the pages.
/// </summary>
public sealed class UsersApiClient
{
    private readonly HttpClient _httpClient;

    public UsersApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UserSearchResultDto?> SearchUserAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/users/search?query={Uri.EscapeDataString(identifier)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserSearchResultDto>(cancellationToken: cancellationToken);
    }

    public async Task<UserSearchResultDto?> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/users/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserSearchResultDto>(cancellationToken: cancellationToken);
    }

    public async Task SetPublicKeyAsync(string publicKeyBase64, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync("api/users/me/public-key", new SetPublicKeyRequest(publicKeyBase64), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetEncryptionKeyAsync(
        string publicKeyBase64, WrappedPrivateKeyDto wrappedPrivateKey, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            "api/users/me/encryption-key", new SetEncryptionKeyRequest(publicKeyBase64, wrappedPrivateKey), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Null means the signed-in account has never backed up a private key - either it predates this
    /// feature, or every browser that ever held one only had a non-extractable (un-backupable) key.
    /// </summary>
    public async Task<WrappedPrivateKeyDto?> GetWrappedPrivateKeyAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/users/me/encryption-key", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WrappedPrivateKeyDto>(cancellationToken: cancellationToken);
    }

    /// <summary>The signed-in account's own profile - everything under /me is scoped to the caller's token.</summary>
    public async Task<AccountDto?> GetAccountAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<AccountDto>("api/users/me", cancellationToken);

    /// <summary>Null on success, otherwise the reason to show the user (currently only a taken username).</summary>
    public async Task<string?> UpdateProfileAsync(
        string displayName, string userName, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            "api/users/me/profile", new UpdateProfileRequest(displayName, userName), cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return "This username is already taken.";
        }

        response.EnsureSuccessStatusCode();
        return null;
    }

    /// <summary>Null on success, otherwise the reason to show the user.</summary>
    public async Task<string?> LinkGoogleAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/users/me/google", new GoogleSignInRequest(idToken), cancellationToken);
        return await ToLinkErrorAsync(response, "Couldn't verify that Google account.", cancellationToken);
    }

    /// <summary>Null on success, otherwise the reason - notably refused when Google is the only way into the account.</summary>
    public async Task<string?> UnlinkGoogleAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync("api/users/me/google", cancellationToken);
        return await ToLinkErrorAsync(response, "Couldn't disconnect Google.", cancellationToken);
    }

    private static async Task<string?> ToLinkErrorAsync(
        HttpResponseMessage response, string unauthorizedMessage, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return unauthorizedMessage;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await response.Content.ReadFromJsonAsync<MessageDto>(cancellationToken: cancellationToken);
            return problem?.Message ?? "That didn't work.";
        }

        response.EnsureSuccessStatusCode();
        return null;
    }

    /// <summary>The shape the API's Conflict responses use for a human-readable reason.</summary>
    private sealed record MessageDto(string Message);

    /// <summary>Sets the first password on an account that has none. False when it already has one.</summary>
    public async Task<bool> SetPasswordAsync(string newPassword, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/users/me/password", new SetPasswordRequest(newPassword), cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>False when the current password doesn't match.</summary>
    public async Task<bool> ChangePasswordAsync(
        string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            "api/users/me/password", new ChangePasswordRequest(currentPassword, newPassword), cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>False when the password doesn't match. On success, every row this account owns is gone server-side.</summary>
    public async Task<bool> DeleteAccountAsync(string password, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "api/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(password))
        };
        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>Null on success, otherwise the reason (the address already belongs to another account).</summary>
    public async Task<string?> RequestEmailVerificationAsync(string emailAddress, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/users/me/email-verification", new RequestEmailVerificationRequest(emailAddress), cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return "An account with this email address already exists.";
        }

        response.EnsureSuccessStatusCode();
        return null;
    }

    /// <summary>False when the code is wrong, expired, already used, or out of attempts.</summary>
    public async Task<bool> ConfirmEmailVerificationAsync(string code, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/users/me/email-verification/confirm", new ConfirmEmailVerificationRequest(code), cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>Records where the caller is. The address is best-effort - a point without one is still worth keeping.</summary>
    public async Task SaveOwnLocationAsync(
        double latitude, double longitude, string? address, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            "api/users/me/location", new SaveOwnLocationRequest(address, latitude, longitude), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Forgets the recorded location. Orbit keeps no history, so this leaves nothing behind.</summary>
    public async Task ClearOwnLocationAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync("api/users/me/location", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
