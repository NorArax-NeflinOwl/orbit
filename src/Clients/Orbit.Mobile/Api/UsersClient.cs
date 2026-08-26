using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Users;

namespace Orbit.Mobile.Api;

/// <summary>
/// Looking up somebody who isn't a contact. Group chat needs this and one-to-one chat did not: a group
/// can hold members the user has never had a conversation with, so the contact list - which is exactly
/// the people they have - does not cover them, and their public key has to be asked for by id.
/// </summary>
public sealed class UsersClient
{
    private readonly HttpClient _httpClient;

    public UsersClient(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>Null when no such account exists - a member of a group whose account was since deleted.</summary>
    public async Task<UserSearchResultDto?> FindAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"api/users/{userId}", cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserSearchResultDto>(cancellationToken);
    }
}
