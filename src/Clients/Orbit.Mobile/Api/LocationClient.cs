using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Users;

namespace Orbit.Mobile.Api;

/// <summary>
/// The location half of the API. Two different things live here and they are worth telling apart.
///
/// The account's <b>own</b> position is stored in the clear - it is the reader's own record of where
/// they are, readable only through their own account (see UserEndpoints: there is no endpoint for
/// reading anybody else's). A position <b>shared</b> with somebody is sealed for exactly them before it
/// leaves the device, so the server relays what it cannot open, the same as a message.
/// </summary>
public sealed class LocationClient
{
    private readonly HttpClient _httpClient;

    public LocationClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task SaveOwnAsync(
        double latitude, double longitude, string? address, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            "api/users/me/location", new SaveOwnLocationRequest(address, latitude, longitude), cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task ClearOwnAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync("api/users/me/location", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Shares one sealed position with one recipient, replacing whatever they had before.
    /// <paramref name="isContinuous"/> only records the intent: nothing on the server keeps refreshing,
    /// the sharer's own device does, and stopping is a separate call.
    /// </summary>
    public async Task ShareAsync(
        Guid recipientUserId, string ciphertextBase64, string nonceBase64, bool isContinuous,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            "api/users/me/location/shares",
            new ShareLocationRequest(recipientUserId, ciphertextBase64, nonceBase64, isContinuous),
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>Stops sharing with one person. The row goes, so a position nobody shares is not stored.</summary>
    public async Task StopSharingAsync(Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync(
            $"api/users/me/location/shares/{recipientUserId}", cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task StopSharingWithEverybodyAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync("api/users/me/location/shares", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Who the caller is currently sharing with.</summary>
    public Task<IReadOnlyList<SharedLocationDto>> GetOwnSharesAsync(CancellationToken cancellationToken = default)
        => ReadSharesAsync("api/users/me/location/shares", cancellationToken);

    /// <summary>What other people are sharing with the caller, as ciphertext only they can open.</summary>
    public Task<IReadOnlyList<SharedLocationDto>> GetSharedWithMeAsync(CancellationToken cancellationToken = default)
        => ReadSharesAsync("api/users/me/location/shared-with-me", cancellationToken);

    /// <summary>
    /// Empty rather than an exception when the account has not unlocked what the endpoint asks for -
    /// both of these need Contacts as well as Location (see PermissionPolicies in Orbit.Api), and the
    /// map opens for anybody with Location alone. Orbit.Web answers the same way, for the same reason:
    /// a refusal here is not a failure to report, it is the answer - there is nobody to show. Left
    /// throwing, the map read the 403 as "Couldn't reach Orbit just now", which blames the connection
    /// for a server that answered perfectly clearly.
    /// </summary>
    private async Task<IReadOnlyList<SharedLocationDto>> ReadSharesAsync(
        string address, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IReadOnlyList<SharedLocationDto>>(
                address, cancellationToken) ?? [];
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            return [];
        }
    }
}
