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

    private sealed record NominatimReverseGeocodeResponse([property: JsonPropertyName("display_name")] string? DisplayName);
}
