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
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        try
        {
            var requestUri = $"search?format=jsonv2&limit=1&q={Uri.EscapeDataString(address.Trim())}";
            var matches = await _httpClient.GetFromJsonAsync<List<NominatimSearchResult>>(requestUri, cancellationToken);
            if (matches is not [{ } best, ..]
                || !double.TryParse(best.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
                || !double.TryParse(best.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
            {
                return null;
            }

            return new GeocodedPlace(latitude, longitude);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private sealed record NominatimReverseGeocodeResponse([property: JsonPropertyName("display_name")] string? DisplayName);

    /// <summary>Nominatim returns the coordinates as strings, which is why they are parsed rather than bound.</summary>
    private sealed record NominatimSearchResult(
        [property: JsonPropertyName("lat")] string? Latitude,
        [property: JsonPropertyName("lon")] string? Longitude);
}

/// <summary>Where an address turned out to be - see <see cref="GeocodingApiClient.FindPlaceAsync"/>.</summary>
public sealed record GeocodedPlace(double Latitude, double Longitude);
