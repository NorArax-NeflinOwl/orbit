using System.Net.Http.Json;
using System.Text.Json;
using Orbit.Core.Transfer;
using Orbit.Core.Transfer.ImportArchive;

namespace Orbit.Mobile.Api;

/// <summary>
/// Everything one account holds, out to a file and back. The archive is its own shape rather than a
/// bundle of the API's DTOs, so a file saved last month keeps opening - see <see cref="OrbitArchive"/>.
///
/// Deliberately not part of the sync spine: this is a file somebody keeps, not a copy the app maintains,
/// and importing creates new things rather than restoring old ones.
/// </summary>
public sealed class TransferClient
{
    /// <summary>Case-insensitive, because a file may have been written by something that cased it differently.</summary>
    private static readonly JsonSerializerOptions ArchiveFormat =
        new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    private readonly HttpClient _httpClient;

    public TransferClient(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>
    /// The whole account, or null when the server would not build it. Handed back as the archive rather
    /// than as text, because what gets written is only the parts that were asked for - see ExportChoice.
    /// </summary>
    public Task<OrbitArchive?> ExportAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<OrbitArchive>("api/transfer/export", cancellationToken);

    /// <summary>The archive as the file holds it. Here rather than at the caller: this is the one place
    /// that knows how an Orbit file is written, and the same shape has to read back in.</summary>
    public string Write(OrbitArchive archive) => JsonSerializer.Serialize(archive, ArchiveFormat);

    /// <summary>
    /// Reads a file back into the account. Null when the text is not an Orbit export at all - which
    /// covers a file that is not JSON and JSON of some other shape, since neither is something the
    /// reader can act on differently.
    /// </summary>
    public async Task<ImportArchiveResult?> ImportAsync(string json, CancellationToken cancellationToken = default)
    {
        OrbitArchive? archive;
        try
        {
            archive = JsonSerializer.Deserialize<OrbitArchive>(json, ArchiveFormat);
        }
        catch (JsonException)
        {
            return null;
        }

        // A version this reader does not know is refused rather than guessed at - see OrbitArchive.
        // It also catches the case JSON alone cannot: any object at all deserialises into this shape
        // with its fields left empty, and such a file would otherwise be sent to the server as an
        // archive of nothing.
        if (archive is not { Version: > 0 and <= OrbitArchive.CurrentVersion })
        {
            return null;
        }

        using var response = await _httpClient.PostAsJsonAsync("api/transfer/import", archive, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ImportArchiveResult>(cancellationToken)
            : null;
    }
}
