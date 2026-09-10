using System.Net.Http.Json;

namespace Orbit.Mobile.Api;

/// <summary>
/// The half of a place this phone has any use for: taking up an offer of one, and asking whether an
/// offer was already taken.
///
/// There is no places screen here yet - a place is met on the map, and the phone has no map of the
/// reader's own places. What it does have is the conversation an offer arrives in, and an offer nobody
/// can answer reads as a blob of JSON, which is the state notes were in before SharedItemInvitation
/// learned to recognise them. Accepting puts the place on the reader's account, where the browser shows
/// it; the screen for it here is a separate piece of work.
/// </summary>
public sealed class PlacesClient
{
    private readonly HttpClient _httpClient;

    public PlacesClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
}
