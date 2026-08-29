using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Orbit.Mobile.Location;

/// <summary>
/// One answer to a search for an address. <paramref name="Name"/> is the full written form, which is
/// what a reader picks by - several answers to one search look alike until their towns are read.
/// </summary>
public sealed record FoundPlace(string Name, double Latitude, double Longitude);

/// <summary>
/// Where an address written by hand actually is, so it can be put on a map. Knowing the address and not
/// where it sits is the ordinary case, and a map that can only be pointed at has no answer for it.
///
/// Talks to OpenStreetMap's free Nominatim endpoint, as Orbit.Web's GeocodingApiClient does, and is
/// given an HttpClient of its own for the same reason: it is a third-party host, so Orbit's own bearer
/// token must never be attached to it. Nominatim's usage policy caps this to light, non-commercial
/// traffic (https://operations.osmfoundation.org/policies/nominatim/).
/// </summary>
public sealed class PlaceSearch
{
    private readonly HttpClient _httpClient;

    public PlaceSearch(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>
    /// The places an address could mean, so somebody looking one up can say which they meant. Several
    /// rather than one because street names repeat: "Długa 4" is a real address in a dozen towns, and
    /// quietly taking the first would drop a pin in whichever of them Nominatim happened to rank first.
    ///
    /// Empty when nothing matches or the lookup fails - both read as "nothing found for that", which is
    /// the truth either way and leaves whatever was already typed alone.
    /// </summary>
    public async Task<IReadOnlyList<FoundPlace>> SearchAsync(
        string address, int limit = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return [];
        }

        try
        {
            var requestUri = $"search?format=jsonv2&limit={limit}&q={Uri.EscapeDataString(address.Trim())}";
            var matches = await _httpClient.GetFromJsonAsync<List<NominatimSearchResult>>(requestUri, cancellationToken);
            return [.. (matches ?? []).Select(ToFoundPlace).OfType<FoundPlace>()];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return [];
        }
    }

    /// <summary>A match whose coordinates will not parse is no match at all - see NominatimSearchResult.</summary>
    private static FoundPlace? ToFoundPlace(NominatimSearchResult match)
        => double.TryParse(match.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
            && double.TryParse(match.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)
                ? new FoundPlace(match.DisplayName ?? string.Empty, latitude, longitude)
                : null;

    /// <summary>Nominatim returns the coordinates as strings, which is why they are parsed rather than bound.</summary>
    private sealed record NominatimSearchResult(
        [property: JsonPropertyName("lat")] string? Latitude,
        [property: JsonPropertyName("lon")] string? Longitude,
        [property: JsonPropertyName("display_name")] string? DisplayName);
}
