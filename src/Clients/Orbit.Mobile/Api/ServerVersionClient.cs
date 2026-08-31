using System.Net.Http.Json;
using Orbit.Contracts.Config;

namespace Orbit.Mobile.Api;

/// <summary>
/// Which build of the server this app is talking to, for the About row - see ServerVersionDto.
///
/// Worth asking at all because the two drift by design here: the phone is released separately and
/// updated whenever its owner chooses, which is the same reason the version gate exists. Showing only
/// the app's own version answers "which Orbit is this" with half the truth.
///
/// Cached, because it cannot change without the server restarting and the row is drawn every time the
/// menu opens.
/// </summary>
public sealed class ServerVersionClient
{
    /// <summary>Short, like the version gate's: this is a line in a menu, not something to wait for.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private ServerVersionDto? _cached;

    public ServerVersionClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Null when the server cannot be reached, which is the ordinary case on a phone: an offline About
    /// row should say nothing about the server rather than guess, and the app's own version is still
    /// worth showing on its own.
    /// </summary>
    public async Task<ServerVersionDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attempt.CancelAfter(Timeout);

        try
        {
            _cached = await _httpClient.GetFromJsonAsync<ServerVersionDto>("api/config/version", attempt.Token);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return null;
        }

        return _cached;
    }
}
