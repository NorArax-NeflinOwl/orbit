using System.Net;

namespace Orbit.Mobile.Api;

/// <summary>
/// Fetches the bytes of a note's pictures - see Orbit.Api's NotePictureEndpoints. Only reading: the
/// phone does not put pictures into a note yet (see info/future-plan.md). A sealed picture comes back
/// sealed, exactly as it was stored; opening it is the caller's, with the account's key.
/// </summary>
public sealed class NotePicturesClient
{
    private readonly HttpClient _httpClient;

    public NotePicturesClient(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>The bytes as stored, or null when the server has no such picture (or the note is not this account's).</summary>
    public Task<byte[]?> DownloadAsync(Guid noteId, Guid pictureId, CancellationToken cancellationToken = default)
        => DownloadAsync($"api/notes/{noteId}/pictures/{pictureId}", cancellationToken);

    /// <summary>The same for a picture of a note somebody was sent a link to - never a sealed one.</summary>
    public Task<byte[]?> DownloadSharedAsync(string token, Guid pictureId, CancellationToken cancellationToken = default)
        => DownloadAsync($"api/public/{Uri.EscapeDataString(token)}/pictures/{pictureId}", cancellationToken);

    private async Task<byte[]?> DownloadAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}
