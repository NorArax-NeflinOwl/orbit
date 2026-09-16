using Orbit.Mobile.Api;
using Orbit.Mobile.Crypto;

namespace Orbit.Mobile.Data;

/// <summary>
/// A note's pictures on the handset, fetched once and read from a directory after that, so a note
/// opened offline still shows what it showed online. The bytes are kept <em>as the server holds them</em>:
/// a private note's picture stays sealed in the directory and is opened into memory each time it is
/// shown, for the reason LocalNote keeps a private note's words sealed - a file lifted off the handset
/// says no more than the server does.
///
/// The directory is the app's cache directory (see MauiProgram), which the OS may clear when it is short
/// of room; a picture cleared that way is fetched again the next time the note is opened online. Nothing
/// here sweeps it - a picture the note no longer names stays until the OS takes it, which is the price of
/// keeping this to one class; see info/future-plan.md.
/// </summary>
public sealed class NotePictureCache
{
    private readonly string _directory;
    private readonly NotePicturesClient _pictures;
    private readonly PrivateContentSealer _privateContent;

    public NotePictureCache(string directory, NotePicturesClient pictures, PrivateContentSealer privateContent)
    {
        _directory = directory;
        _pictures = pictures;
        _privateContent = privateContent;
    }

    /// <summary>
    /// The picture's bytes ready to draw, or null when they cannot be had: not on the handset and no
    /// connection, gone from the server, or sealed under a key this device does not hold. Which of
    /// those it was is the screen's to say, from <see cref="Holds"/> and whether the device has a key; a
    /// picture that cannot be shown is one picture, not a broken note.
    /// </summary>
    public async Task<byte[]?> OpenAsync(Guid noteId, Guid pictureId, bool isSealed, CancellationToken cancellationToken = default)
    {
        var stored = await FetchAsync(pictureId, () => _pictures.DownloadAsync(noteId, pictureId, cancellationToken), cancellationToken);
        if (stored is null || !isSealed)
        {
            return stored;
        }

        try
        {
            using var key = await _privateContent.UnlockAsync(cancellationToken);
            return key.OpenBytes(stored);
        }
        catch (EncryptionKeyLockedException)
        {
            return null;
        }
    }

    /// <summary>A picture of a note somebody was sent a link to - never sealed, so what is fetched is what is drawn.</summary>
    public Task<byte[]?> OpenSharedAsync(string token, Guid pictureId, CancellationToken cancellationToken = default)
        => FetchAsync(pictureId, () => _pictures.DownloadSharedAsync(token, pictureId, cancellationToken), cancellationToken);

    /// <summary>Whether the picture's bytes are on the handset already - what decides between "fetch it online" and the other reasons.</summary>
    public bool Holds(Guid pictureId) => File.Exists(PathOf(pictureId));

    /// <summary>
    /// The bytes as the server holds them: from the directory when they are there, fetched and kept
    /// otherwise. Null when neither can answer - offline with nothing kept, or gone from the server. A
    /// directory that cannot be written to (full, gone) costs the keeping, not the showing.
    /// </summary>
    private async Task<byte[]?> FetchAsync(Guid pictureId, Func<Task<byte[]?>> download, CancellationToken cancellationToken)
    {
        var path = PathOf(pictureId);
        if (File.Exists(path))
        {
            return await File.ReadAllBytesAsync(path, cancellationToken);
        }

        byte[]? fetched;
        try
        {
            fetched = await download();
        }
        catch (HttpRequestException)
        {
            return null;
        }

        if (fetched is null)
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(_directory);
            await File.WriteAllBytesAsync(path, fetched, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Shown this once and fetched again next time, rather than not shown at all.
        }

        return fetched;
    }

    /// <summary>Named by the picture's id alone: the id is unique across notes, and the kind of picture travels in the line, not the file.</summary>
    private string PathOf(Guid pictureId) => Path.Combine(_directory, pictureId.ToString("N"));
}
