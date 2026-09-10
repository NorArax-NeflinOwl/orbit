using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Places;
using Orbit.Contracts.Sharing;

namespace Orbit.Web.Services;

/// <summary>
/// The places this account keeps, and the ones somebody handed over - see Orbit.Core.Places.Place.
///
/// A place is sealed unless its owner said otherwise, so unlike the other clients here this one seals on
/// nearly every save and opens on nearly every read. What the server holds for a sealed place is
/// ciphertext and three empty fields; what a screen gets back from here is the place itself.
/// </summary>
public sealed class PlacesApiClient
{
    /// <summary>Shown in place of a sealed place nobody can open any more - see PrivateContentSealer.OpenAsync.</summary>
    private const string UnreadablePlaceName = "Unreadable - encrypted with an older key";

    private readonly HttpClient _httpClient;
    private readonly PrivateContentSealer? _privateContentSealer;
    private readonly Translations? _translations;

    public PlacesApiClient(
        HttpClient httpClient, PrivateContentSealer? privateContentSealer = null, Translations? translations = null)
    {
        _httpClient = httpClient;
        _privateContentSealer = privateContentSealer;
        _translations = translations;
    }

    public async Task<IReadOnlyList<PlaceDto>> GetPlacesAsync(CancellationToken cancellationToken = default)
    {
        var places = await _httpClient.GetFromJsonAsync<List<PlaceDto>>("api/places", cancellationToken) ?? [];
        var opened = new List<PlaceDto>(places.Count);
        foreach (var place in places)
        {
            opened.Add(await OpenIfSealedAsync(place, cancellationToken));
        }

        return opened;
    }

    /// <summary>Null for an id this account has no place under, which is what a stale link reads as.</summary>
    public async Task<PlaceDto?> GetPlaceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/places/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var place = await response.Content.ReadFromJsonAsync<PlaceDto>(cancellationToken: cancellationToken);
        return place is null ? null : await OpenIfSealedAsync(place, cancellationToken);
    }

    /// <summary>
    /// Hands back an open place unchanged, and a sealed one with its name, description and point put
    /// back. One this browser holds no key for keeps its empty fields and says so in the name rather
    /// than throwing, so a single unreadable place does not take the whole map down with it.
    /// </summary>
    private async Task<PlaceDto> OpenIfSealedAsync(PlaceDto place, CancellationToken cancellationToken)
    {
        if (!place.IsPrivate || place.EncryptedContent is not { } sealedContent || _privateContentSealer is null)
        {
            return place;
        }

        var opened = await _privateContentSealer.OpenAsync<SealedPlace>(sealedContent, cancellationToken);
        return opened is null
            ? place with { Name = Translated(UnreadablePlaceName) }
            : place with { Name = opened.Name, Description = opened.Description, Where = opened.Where };
    }

    /// <summary>
    /// Seals a place's name, description and point and empties the readable fields, so what leaves this
    /// browser matches what the server is allowed to hold. Left alone for a place its owner chose to
    /// leave open.
    /// </summary>
    private async Task<SavePlaceRequest> SealIfPrivateAsync(
        SavePlaceRequest request, CancellationToken cancellationToken)
    {
        if (!request.IsPrivate)
        {
            return request with { EncryptedContent = null };
        }

        if (_privateContentSealer is null)
        {
            throw new InvalidOperationException(
                "This PlacesApiClient was built without a PrivateContentSealer, so it can't save a private place.");
        }

        var sealedContent = await _privateContentSealer.SealAsync(
            new SealedPlace(request.Name, request.Description, request.Where), cancellationToken);

        return request with
        {
            Name = string.Empty,
            Description = string.Empty,
            Where = new EventLocationDto(string.Empty, 0, 0),
            EncryptedContent = sealedContent
        };
    }

    private string Translated(string text) => _translations is null ? text : _translations[text];

    /// <summary>The new place's id.</summary>
    public async Task<Guid> CreatePlaceAsync(SavePlaceRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/places", await SealIfPrivateAsync(request, cancellationToken), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    /// <summary>False when there is nothing of this account's under that id - see UpdatePlaceCommand.</summary>
    public async Task<bool> UpdatePlaceAsync(
        Guid id, SavePlaceRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/places/{id}", await SealIfPrivateAsync(request, cancellationToken), cancellationToken);
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
