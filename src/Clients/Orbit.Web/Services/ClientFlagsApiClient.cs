using System.Net.Http.Json;
using Orbit.Contracts.Config;

namespace Orbit.Web.Services;

/// <summary>
/// Reads the server-environment flags the client can't work out for itself (see ConfigEndpoints).
/// Unauthenticated and unchanging for the lifetime of the app, so the first answer is cached and reused -
/// several unrelated places ask for these, and none of them should each cost a round trip.
/// </summary>
public sealed class ClientFlagsApiClient
{
    private readonly HttpClient _httpClient;
    private ClientFlagsDto? _cachedFlags;

    public ClientFlagsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>Empty when this deployment has no Google sign-in configured, which hides the button entirely.</summary>
    public async Task<string> GetGoogleClientIdAsync(CancellationToken cancellationToken = default)
        => (await GetFlagsAsync(cancellationToken)).GoogleClientId;

    /// <summary>Defaults to "not allowed" if the call fails, matching the fail-closed intent of the flag.</summary>
    public async Task<bool> GetExceptionDetailsAllowedAsync(CancellationToken cancellationToken = default)
        => (await GetFlagsAsync(cancellationToken)).ExceptionDetailsAllowed;

    private async Task<ClientFlagsDto> GetFlagsAsync(CancellationToken cancellationToken)
    {
        if (_cachedFlags is not null)
        {
            return _cachedFlags;
        }

        try
        {
            _cachedFlags = await _httpClient.GetFromJsonAsync<ClientFlagsDto>("api/config/client-flags", cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Not cached, so a transient failure is retried on the next ask rather than remembered.
            return new ClientFlagsDto(ExceptionDetailsAllowed: false, GoogleClientId: string.Empty);
        }

        return _cachedFlags ?? new ClientFlagsDto(ExceptionDetailsAllowed: false, GoogleClientId: string.Empty);
    }
}
