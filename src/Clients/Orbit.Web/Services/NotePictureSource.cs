using Microsoft.JSInterop;
using Orbit.Contracts.Notes;

namespace Orbit.Web.Services;

/// <summary>
/// Turns a picture named on a note's line into something an <c>&lt;img&gt;</c> can draw. The bytes are
/// fetched through the app's own client (an <c>&lt;img src="/api/…"&gt;</c> sends no bearer token), a
/// sealed picture's are opened here with the key the note itself is opened with, and what the document
/// gets is a <c>blob:</c> URL - which is also the only way a sealed picture can ever be drawn, since the
/// server holds ciphertext.
///
/// One URL per picture for the life of the page, revoked together when the page goes: a note is drawn
/// many times while it is read and edited, and fetching a photograph on every render is not a cache
/// miss but a bill.
/// </summary>
public sealed class NotePictureSource : IAsyncDisposable
{
    private readonly NotePicturesApiClient _pictures;
    private readonly PrivateContentSealer _sealer;
    private readonly IJSRuntime _jsRuntime;
    private readonly Dictionary<Guid, string?> _urls = [];
    private IJSObjectReference? _objectUrls;

    public NotePictureSource(NotePicturesApiClient pictures, PrivateContentSealer sealer, IJSRuntime jsRuntime)
    {
        _pictures = pictures;
        _sealer = sealer;
        _jsRuntime = jsRuntime;
    }

    /// <summary>A URL for a picture of a note the reader may see, or null when the bytes cannot be had or opened.</summary>
    public Task<string?> UrlForAsync(Guid noteId, NotePictureLineDto picture, bool isSealed, CancellationToken cancellationToken = default)
        => UrlForAsync(picture, () => _pictures.DownloadAsync(noteId, picture.PictureId, cancellationToken), isSealed, cancellationToken);

    /// <summary>The same for a picture of a note behind a share link, which is never sealed.</summary>
    public Task<string?> UrlForSharedAsync(string token, NotePictureLineDto picture, CancellationToken cancellationToken = default)
        => UrlForAsync(picture, () => _pictures.DownloadSharedAsync(token, picture.PictureId, cancellationToken), isSealed: false, cancellationToken);

    private async Task<string?> UrlForAsync(
        NotePictureLineDto picture, Func<Task<byte[]?>> download, bool isSealed, CancellationToken cancellationToken)
    {
        if (_urls.TryGetValue(picture.PictureId, out var known))
        {
            return known;
        }

        var bytes = await download();
        if (bytes is not null && isSealed)
        {
            bytes = await _sealer.OpenBytesAsync(bytes, cancellationToken);
        }

        var url = bytes is null ? null : await CreateUrlAsync(bytes, picture.ContentType, cancellationToken);
        _urls[picture.PictureId] = url;
        return url;
    }

    private async Task<string> CreateUrlAsync(byte[] bytes, string contentType, CancellationToken cancellationToken)
    {
        _objectUrls ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, "./js/objectUrls.js");
        return await _objectUrls.InvokeAsync<string>("create", cancellationToken, bytes, contentType);
    }

    public async ValueTask DisposeAsync()
    {
        if (_objectUrls is null)
        {
            return;
        }

        try
        {
            foreach (var url in _urls.Values.Where(url => url is not null))
            {
                await _objectUrls.InvokeVoidAsync("revoke", url);
            }

            await _objectUrls.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The page is already gone, and its URLs with it.
        }
    }
}
