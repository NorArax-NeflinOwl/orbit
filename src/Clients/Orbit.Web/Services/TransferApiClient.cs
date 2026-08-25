using System.Net.Http.Json;
using Orbit.Core.Transfer;
using Orbit.Core.Transfer.ImportArchive;

namespace Orbit.Web.Services;

/// <summary>
/// Wraps /api/transfer. Works in the archive's own shape rather than a client-side copy of it: there is
/// exactly one definition of what a saved file contains, and both ends read it.
/// </summary>
public sealed class TransferApiClient
{
    private readonly HttpClient _httpClient;

    public TransferApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<OrbitArchive?> ExportAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<OrbitArchive>("api/transfer/export", cancellationToken);

    /// <summary>
    /// Returns null when the server refused the file - a version it doesn't know, or content it can't
    /// make sense of - which the caller shows as "this file couldn't be read" rather than a stack trace.
    /// </summary>
    public async Task<ImportArchiveResult?> ImportAsync(OrbitArchive archive, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/transfer/import", archive, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ImportArchiveResult>(cancellationToken: cancellationToken)
            : null;
    }
}
