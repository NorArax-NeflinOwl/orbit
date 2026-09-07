using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Sharing;
using Orbit.Core.Notifications;

namespace Orbit.Web.Services;

/// <summary>
/// Reads one offer somebody made to this reader - what the invitation page shows. Accepting is not here
/// and deliberately so: each kind is accepted at its own endpoint, which is where that kind's rules
/// live, and this client would only be a fifth way to reach the same four.
/// </summary>
public sealed class SharesApiClient
{
    private readonly HttpClient _httpClient;

    public SharesApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// The offer, or null when there is none for this reader - a withdrawn one and one that was never
    /// theirs answer the same way, which is the server's own rule (see ShareOfferEndpoints).
    /// </summary>
    public async Task<ShareOfferDto?> GetOfferAsync(
        SharedItemKind kind, Guid shareId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/shares/{SharedItemPath.For(kind)}/{shareId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ShareOfferDto>(cancellationToken: cancellationToken);
    }
}
