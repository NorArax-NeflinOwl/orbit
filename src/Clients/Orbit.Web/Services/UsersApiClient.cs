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
}
