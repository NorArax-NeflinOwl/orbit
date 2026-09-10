using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Places;
using Orbit.Contracts.Sharing;
using Orbit.Contracts.Sync;

namespace Orbit.Mobile.Api;

/// <summary>
/// The places half of the API. Only the synchroniser and the sharing panel call this - screens read the
/// local database, and the sync layer keeps the two in step (see info/orbit-maui-plan.md §5).
/// </summary>
public sealed class PlacesClient
{
    private readonly HttpClient _httpClient;

    public PlacesClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<IReadOnlyList<PlaceDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<PlaceDto>>("api/places", cancellationToken) ?? [];

    /// <inheritdoc cref="NotesClient.GetChangesAsync"/>
    public async Task<ChangeFeedDto<PlaceDto>> GetChangesAsync(
        string? cursor, CancellationToken cancellationToken = default)
    {
        var since = cursor ?? DateTimeOffset.MinValue.UtcDateTime.ToString("O");
        return await _httpClient.GetFromJsonAsync<ChangeFeedDto<PlaceDto>>(
            $"api/places/changes?since={Uri.EscapeDataString(since)}", cancellationToken)
            ?? new ChangeFeedDto<PlaceDto>([], [], since);
    }

    /// <summary>The id the server assigned, which is what makes the local place reachable from anywhere else.</summary>
    public async Task<Guid> CreateAsync(SavePlaceRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/places", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken);
    }

    public async Task<WriteOutcome> UpdateAsync(
        Guid placeId, SavePlaceRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/places/{placeId}", request, cancellationToken);
        return ReadOutcome(response);
    }

    public async Task<WriteOutcome> DeleteAsync(Guid placeId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/places/{placeId}", cancellationToken);
        // A place already gone is the outcome the caller wanted, not a failure to retry.
        return response.StatusCode is HttpStatusCode.NotFound ? WriteOutcome.Applied : ReadOutcome(response);
    }

    /// <inheritdoc cref="NotesClient.ShareAsync"/>
    public async Task<ShareResultDto?> ShareAsync(
        Guid placeId, Guid recipientUserId, string accessLevel, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/places/{placeId}/shares", new SharePlaceRequest(recipientUserId, accessLevel), cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ShareResultDto>(cancellationToken)
            : null;
    }

    /// <inheritdoc cref="NotesClient.AcceptShareAsync"/>
    public async Task<bool> AcceptShareAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"api/places/shares/{shareId}/accept", null, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <inheritdoc cref="InventoryClient.IsShareAcceptedAsync"/>
    public async Task<bool?> IsShareAcceptedAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"api/places/shares/{shareId}/status", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<bool>(cancellationToken)
            : null;
    }

    /// <inheritdoc cref="NotesClient.ReadOutcome"/>
    private static WriteOutcome ReadOutcome(HttpResponseMessage response)
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.Conflict:
            case HttpStatusCode.Forbidden:
                return WriteOutcome.Refused;
            case HttpStatusCode.NotFound:
                return WriteOutcome.Gone;
            default:
                response.EnsureSuccessStatusCode();
                return WriteOutcome.Applied;
        }
    }
}
