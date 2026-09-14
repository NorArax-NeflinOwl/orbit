using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Orbit.Contracts.Notes;

namespace Orbit.Web.Services;

/// <summary>
/// Orbit.Api's /api/notes/{id}/pictures - a note's pictures, uploaded one request each and fetched by
/// id. Bytes in and bytes out, nothing opened or sealed here: that is NotePictureSource's and the
/// editor's, which know whether the note is private. The public half, a picture behind a share link,
/// is here too because it is the same bytes by another door.
/// </summary>
public sealed class NotePicturesApiClient
{
    /// <summary>The header a sealed upload carries - see NotePictureEndpoints.SealedHeader in Orbit.Api.</summary>
    private const string SealedHeader = "X-Orbit-Picture-Sealed";

    private readonly HttpClient _httpClient;

    public NotePicturesApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Uploads one picture. Answers the picture as stored, or one of the reasons it was not: the note
    /// holds all it may (<see cref="NotePictureUploadOutcome.TooLarge"/>), or it is not the caller's to
    /// change. <paramref name="isSealed"/> says the bytes are ciphertext, and then their kind travels
    /// on the line that will name them rather than in a header the server could read.
    /// </summary>
    public async Task<NotePictureUploadOutcome> UploadAsync(
        Guid noteId, byte[] bytes, string contentType, bool isSealed, CancellationToken cancellationToken = default)
    {
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(isSealed ? "application/octet-stream" : contentType);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/notes/{noteId}/pictures") { Content = content };
        if (isSealed)
        {
            request.Headers.Add(SealedHeader, "true");
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
        {
            return NotePictureUploadOutcome.TooLarge;
        }

        if (!response.IsSuccessStatusCode)
        {
            return NotePictureUploadOutcome.Refused;
        }

        var stored = await response.Content.ReadFromJsonAsync<NotePictureDto>(cancellationToken);
        return stored is null ? NotePictureUploadOutcome.Refused : NotePictureUploadOutcome.Stored(stored);
    }

    /// <summary>The bytes as stored - ciphertext for a sealed picture - or null when the note or the picture cannot be read.</summary>
    public Task<byte[]?> DownloadAsync(Guid noteId, Guid pictureId, CancellationToken cancellationToken = default)
        => DownloadAsync($"api/notes/{noteId}/pictures/{pictureId}", cancellationToken);

    /// <summary>The same for a picture of a note somebody was sent a link to - never a sealed one.</summary>
    public Task<byte[]?> DownloadSharedAsync(string token, Guid pictureId, CancellationToken cancellationToken = default)
        => DownloadAsync($"api/public/{Uri.EscapeDataString(token)}/pictures/{pictureId}", cancellationToken);

    public async Task<bool> DeleteAsync(Guid noteId, Guid pictureId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync($"api/notes/{noteId}/pictures/{pictureId}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private async Task<byte[]?> DownloadAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(cancellationToken) : null;
    }
}

/// <summary>What an upload came to - see <see cref="NotePicturesApiClient.UploadAsync"/>.</summary>
public sealed record NotePictureUploadOutcome(NotePictureUploadOutcomeKind Kind, NotePictureDto? Picture = null)
{
    public static NotePictureUploadOutcome TooLarge { get; } = new(NotePictureUploadOutcomeKind.TooLarge);

    public static NotePictureUploadOutcome Refused { get; } = new(NotePictureUploadOutcomeKind.Refused);

    public static NotePictureUploadOutcome Stored(NotePictureDto picture) => new(NotePictureUploadOutcomeKind.Stored, picture);
}

public enum NotePictureUploadOutcomeKind
{
    Stored,
    TooLarge,
    Refused
}
