using System.Net.Http.Json;
using Orbit.Contracts.Config;

namespace Orbit.Web.Services;

/// <summary>
/// Reads the server-environment flags the client can't work out for itself (see ConfigEndpoints).
/// Unchanging for the lifetime of the app, so the first answer is cached and reused - several unrelated
/// places ask for these, and none of them should each cost a round trip.
///
/// The version is the one answer that is not only about the server: how much of it comes back depends
/// on what the caller is allowed to see, so it carries this session's token and can be forgotten when
/// somebody signs in - see <see cref="ForgetTheServerVersion"/>.
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

    /// <summary>
    /// Which build of the server this client is talking to. Null when it cannot be asked - an offline
    /// footer should say nothing about the server rather than guess, and the client's own version is
    /// still worth showing on its own.
    ///
    /// Cached like the flags: it cannot change without the server restarting, and a footer drawn on
    /// every page must not cost a round trip each time. What the answer contains can change, though -
    /// see <see cref="ForgetTheServerVersion"/>.
    /// </summary>
    public async Task<ServerVersionDto?> GetServerVersionAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedServerVersion is not null)
        {
            return _cachedServerVersion;
        }

        try
        {
            _cachedServerVersion = await _httpClient.GetFromJsonAsync<ServerVersionDto>(
                "api/config/version", cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return null;
        }

        return _cachedServerVersion;
    }

    /// <summary>
    /// Drops the remembered answer, so the next ask is made as whoever is signed in now. The server
    /// sends its commit only to an account holding Debug (see ConfigEndpoints), and the footer is drawn
    /// once before anybody has signed in - without this it would keep the answer given to nobody.
    /// </summary>
    public void ForgetTheServerVersion() => _cachedServerVersion = null;

    private ServerVersionDto? _cachedServerVersion;

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
