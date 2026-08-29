using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Orbit.Web.Services;

/// <summary>
/// Resolves a human-readable address for a pair of coordinates via OpenStreetMap's free Nominatim
/// reverse-geocoding endpoint - no API key needed. Best-effort only, and deliberately not routed
/// through AuthorizationMessageHandler: this is a third-party host, so Orbit's own bearer token must
/// never be attached to it. Nominatim's usage policy caps this to light, non-commercial traffic
/// (see https://operations.osmfoundation.org/policies/nominatim/) - a deployment with meaningful
/// volume should self-host it instead.
/// </summary>
public sealed class GeocodingApiClient
{
    private readonly HttpClient _httpClient;

    public GeocodingApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Returns the resolved address, or null if Nominatim has nothing for these coordinates (e.g. open
    /// water) or the request fails - the caller falls back to letting the user type an address in by
    /// hand rather than blocking on this.
    /// </summary>
    public async Task<string?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        try
        {
            var requestUri =
                $"reverse?format=jsonv2&lat={latitude.ToString(CultureInfo.InvariantCulture)}&lon={longitude.ToString(CultureInfo.InvariantCulture)}";
            var response = await _httpClient.GetFromJsonAsync<NominatimReverseGeocodeResponse>(requestUri, cancellationToken);
            return response?.DisplayName;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>
    /// The other direction: where an address written by hand actually is, so it can be put on a map.
    /// Null when Nominatim recognises nothing - a place nobody can find is shown as the words somebody
    /// typed rather than as a pin in the wrong country.
    /// </summary>
    public async Task<GeocodedPlace?> FindPlaceAsync(string address, CancellationToken cancellationToken = default)
    {
        var matches = await SearchPlacesAsync(address, limit: 1, cancellationToken);
        return matches is [{ } best, ..] ? new GeocodedPlace(best.Latitude, best.Longitude) : null;
    }

    /// <summary>
    /// The places an address could mean, so somebody looking one up can say which they meant. Several
    /// rather than one because street names repeat: "Długa 4" is a real address in a dozen towns, and
    /// quietly taking the first would drop a pin in whichever of them Nominatim happened to rank first.
    /// Empty when nothing matches or the lookup fails - both read as "nothing found for that", which is
    /// the truth either way and leaves whatever was already typed alone.
    /// </summary>
    public async Task<IReadOnlyList<FoundPlace>> SearchPlacesAsync(
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
        catch (HttpRequestException)
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

    private sealed record NominatimReverseGeocodeResponse([property: JsonPropertyName("display_name")] string? DisplayName);

    /// <summary>Nominatim returns the coordinates as strings, which is why they are parsed rather than bound.</summary>
    private sealed record NominatimSearchResult(
        [property: JsonPropertyName("lat")] string? Latitude,
        [property: JsonPropertyName("lon")] string? Longitude,
        [property: JsonPropertyName("display_name")] string? DisplayName);
}

/// <summary>Where an address turned out to be - see <see cref="GeocodingApiClient.FindPlaceAsync"/>.</summary>
public sealed record GeocodedPlace(double Latitude, double Longitude);

/// <summary>
/// One answer to a search for an address - see <see cref="GeocodingApiClient.SearchPlacesAsync"/>.
/// <paramref name="Name"/> is Nominatim's full written form of it, which is what a reader picks by.
/// </summary>
public sealed record FoundPlace(string Name, double Latitude, double Longitude);
