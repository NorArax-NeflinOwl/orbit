using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts;
using Orbit.Contracts.Places;
using Orbit.Contracts.Sharing;

namespace Orbit.Web.Services;

/// <summary>
/// The places this account keeps, and the ones somebody handed over - see Orbit.Core.Places.Place.
/// Nothing about a place is ever sealed, so this stays the plainest client in the app: what the server
/// holds is what comes back.
/// </summary>
public sealed class PlacesApiClient
{
    private readonly HttpClient _httpClient;

    public PlacesApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<PlaceDto>> GetPlacesAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<List<PlaceDto>>("api/places", cancellationToken) ?? [];

    /// <summary>Null for an id this account has no place under, which is what a stale link reads as.</summary>
    public async Task<PlaceDto?> GetPlaceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/places/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PlaceDto>(cancellationToken: cancellationToken);
    }

    /// <summary>The new place's id.</summary>
    public async Task<Guid> CreatePlaceAsync(SavePlaceRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/places", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    /// <summary>False when there is nothing of this account's under that id - see UpdatePlaceCommand.</summary>
    public async Task<bool> UpdatePlaceAsync(
        Guid id, SavePlaceRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/places/{id}", request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>The copy's id, or null for a place that answers to nobody here - see DuplicatePlaceCommand.</summary>
    public async Task<Guid?> DuplicatePlaceAsync(
        Guid id, string? name = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/places/{id}/duplicate", new DuplicateRequest(name), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task<bool> DeletePlaceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/places/{id}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Null for a place this account cannot share, which is also what a stale id reads as.</summary>
    public async Task<ShareResultDto?> SharePlaceAsync(
        Guid placeId, Guid recipientUserId, string accessLevel, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/places/{placeId}/shares", new SharePlaceRequest(recipientUserId, accessLevel), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ShareResultDto>(cancellationToken: cancellationToken);
    }

    public async Task<bool> AcceptPlaceShareAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/places/shares/{shareId}/accept", content: null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc cref="InventoryApiClient.GetInventoryShareStatusAsync"/>
    public async Task<bool?> GetPlaceShareStatusAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/places/shares/{shareId}/status", cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
    }
}
