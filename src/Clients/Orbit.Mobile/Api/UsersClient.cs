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

    /// <summary>
    /// Finds somebody by their exact email address or username, so a conversation can be started with
    /// them. Null when there is no such account.
    ///
    /// <b>Exact match only, and deliberately so</b> - the server does no partial or fuzzy matching, which
    /// is what stops the search being used to enumerate the user base by trying prefixes (see
    /// SearchUserQueryHandler). It also never returns the searcher themselves.
    /// </summary>
    public async Task<UserSearchResultDto?> SearchAsync(string identifier, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"api/users/search?query={Uri.EscapeDataString(identifier)}", cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserSearchResultDto>(cancellationToken);
    }

    /// <summary>
    /// What this account has been unlocked for, by <c>ApplicationPermission</c> name. Presentation only
    /// - the server refuses a locked endpoint whatever the phone believes (see PermissionPolicies in
    /// Orbit.Api).
    /// </summary>
    public async Task<IReadOnlyList<string>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var permissions = await _httpClient.GetFromJsonAsync<UserPermissionsDto>(
            "api/users/me/permissions", cancellationToken);

        return permissions?.Granted ?? [];
    }

    /// <summary>What the code unlocked, and what it needed first when it unlocked nothing.</summary>
    public async Task<RedeemPermissionCodeResultDto> RedeemPermissionCodeAsync(
        string code, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/users/me/permissions/redeem", new RedeemPermissionCodeRequest(code), cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RedeemPermissionCodeResultDto>(cancellationToken)
            ?? new RedeemPermissionCodeResultDto(Granted: null);
    }

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
