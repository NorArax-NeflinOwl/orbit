using System.Net.Http.Json;
using Orbit.Contracts.Diagnostics;

namespace Orbit.Mobile.Api;

/// <summary>
/// Sends this device's log to Orbit.
///
/// Only ever called because somebody pressed the button. Nothing here runs on a schedule, and no log
/// leaves the phone on its own - see the plan's §8, which makes that a rule rather than a default.
/// </summary>
public sealed class DiagnosticsClient
{
    private readonly HttpClient _httpClient;

    public DiagnosticsClient(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>
    /// How many entries the server could read out of the file. Zero is a real answer rather than a
    /// failure: a log from a phone that was already misbehaving is often truncated mid-write, and the
    /// server keeps what it can parse.
    /// </summary>
    public async Task<int> UploadAsync(
        UploadDiagnosticLogRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/diagnostics/logs", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var stored = await response.Content.ReadFromJsonAsync<UploadDiagnosticLogResponse>(cancellationToken);
        return stored?.StoredEntryCount ?? 0;
    }
}
