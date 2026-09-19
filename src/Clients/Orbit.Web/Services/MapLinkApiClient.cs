using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Location;

namespace Orbit.Web.Services;

/// <summary>
/// Asks the server where a shortened map link points - the one kind this browser cannot read for
/// itself. Every other kind is read here without asking anybody (MapLinks); a shortener answers a
/// redirect without the headers that would let a page read it, so the question has to be put to
/// somewhere that is not a browser.
/// </summary>
public sealed class MapLinkApiClient
{
    private readonly HttpClient _httpClient;

    public MapLinkApiClient(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>
    /// Where the link points, or null where nobody could say - the shortener is down, the link has
    /// expired, what is behind it is not a map. Null is an ordinary answer here: the page shows the
    /// link the reader was given and says it could not place it.
    /// </summary>
    public async Task<ResolvedMapLinkDto?> ResolveAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"api/location/map-link?url={Uri.EscapeDataString(url)}", cancellationToken);
            return response.StatusCode == HttpStatusCode.NoContent || !response.IsSuccessStatusCode
                ? null
                : await response.Content.ReadFromJsonAsync<ResolvedMapLinkDto>(cancellationToken: cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
