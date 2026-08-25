using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Sharing;

namespace Orbit.Web.Services;

/// <summary>
/// Wraps /api/share-links (the owner's side) and /api/public (the reader's side). The reader's side is
/// the only part of the API that answers without a token, which is why the page behind a link works for
/// someone with no account at all.
/// </summary>
public sealed class PublicShareApiClient
{
    private readonly HttpClient _httpClient;

    public PublicShareApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>Returns the item's existing link if it has one, otherwise makes one. Null when the item isn't the caller's to publish.</summary>
    public async Task<PublicShareLinkDto?> CreateLinkAsync(
        string itemType, Guid itemId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/share-links", new CreatePublicShareLinkRequest(itemType, itemId), cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PublicShareLinkDto>(cancellationToken: cancellationToken)
            : null;
    }

    /// <summary>The item's live link, or null if there isn't one - asked when an editor opens, so it can say whether the item is already published.</summary>
    public async Task<PublicShareLinkDto?> GetLinkAsync(string itemType, Guid itemId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/share-links/{itemType}/{itemId}", cancellationToken);
        return response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadFromJsonAsync<PublicShareLinkDto>(cancellationToken: cancellationToken)
            : null;
    }

    public async Task RevokeLinkAsync(string itemType, Guid itemId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/share-links/{itemType}/{itemId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Null for every way a link can fail - unknown, revoked, or pointing at something gone - which the page shows as one message.</summary>
    public async Task<PublicSharedItemDto?> ReadAsync(string token, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/public/{token}", cancellationToken);
        return response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadFromJsonAsync<PublicSharedItemDto>(cancellationToken: cancellationToken)
            : null;
    }

    public async Task<ClaimPublicShareLinkResponse?> ClaimAsync(string token, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/public/{token}/claim", content: null, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ClaimPublicShareLinkResponse>(cancellationToken: cancellationToken)
            : null;
    }
}
