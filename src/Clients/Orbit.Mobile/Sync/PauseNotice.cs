using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Orbit.Mobile.Sync;

/// <summary>
/// What the deployment left behind to say it is paused on purpose - written by
/// scripts/stop-azure-compute.sh when the cost ceiling stops the server, removed when it is resumed.
/// </summary>
/// <param name="Message">A sentence for the reader, in whatever language the file's author wrote. Empty when none was left.</param>
/// <param name="SinceUtc">When the pause began, or null when the file did not say.</param>
public sealed record PauseNotice(string Message, DateTimeOffset? SinceUtc);

/// <summary>Reads the pause notice from wherever this build was told to look.</summary>
public interface IPauseNoticeReader
{
    /// <summary>The notice, or null when there is none - the deployment is not paused, or nobody told this build where to look.</summary>
    Task<PauseNotice?> ReadAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Reads the notice from a public blob - a small JSON file on the storage account that also serves the
/// APK, which costs pennies and stays up through the stop. Only asked after the API has just failed to
/// answer as itself, so a working day costs no extra request; see <see cref="ServerReachability"/>.
///
/// Absent, malformed or unreachable all read as "no notice": a pause file that cannot be read must not
/// make the app claim a pause, and the API's own answers remain the authority either way. A notice whose
/// <c>paused</c> is false is no notice too, so a file left behind by mistake can be defused by editing
/// rather than only by deleting.
/// </summary>
public sealed class PauseNoticeReader : IPauseNoticeReader
{
    private readonly HttpClient _httpClient;
    private readonly Uri? _address;
    private readonly ILogger<PauseNoticeReader> _logger;

    /// <param name="address">Where the file is, or null for a build that was told nothing - which never reports a pause.</param>
    public PauseNoticeReader(HttpClient httpClient, Uri? address, ILogger<PauseNoticeReader> logger)
    {
        _httpClient = httpClient;
        _address = address;
        _logger = logger;
    }

    public async Task<PauseNotice?> ReadAsync(CancellationToken cancellationToken)
    {
        if (_address is null)
        {
            return null;
        }

        try
        {
            using var response = await _httpClient.GetAsync(_address, cancellationToken);
            if (response.StatusCode is HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var file = await response.Content.ReadFromJsonAsync<PauseNoticeFile>(cancellationToken);
            return file is { Paused: true }
                ? new PauseNotice(file.Message ?? string.Empty, file.Since)
                : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            _logger.LogInformation("Could not read the pause notice ({Reason}); assuming there is none", exception.Message);
            return null;
        }
    }

    /// <summary>The file as the stop script writes it. Field names are the script's, so they are spelled out.</summary>
    private sealed record PauseNoticeFile(
        [property: JsonPropertyName("paused")] bool Paused,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("since")] DateTimeOffset? Since);
}
