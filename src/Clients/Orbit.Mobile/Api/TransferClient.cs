using System.Net.Http.Json;
using System.Text.Json;
using Orbit.Contracts;
using Orbit.Contracts.Places;
using Orbit.Core.Transfer;
using Orbit.Core.Transfer.ImportArchive;
using Orbit.Mobile.Crypto;

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
    private readonly PrivateContentSealer _privateContent;

    public TransferClient(HttpClient httpClient, PrivateContentSealer privateContent)
    {
        _httpClient = httpClient;
        _privateContent = privateContent;
    }

    /// <summary>
    /// The whole account, or null when the server would not build it. Handed back as the archive rather
    /// than as text, because what gets written is only the parts that were asked for - see ExportChoice.
    /// </summary>
    public Task<OrbitArchive?> ExportAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<OrbitArchive>("api/transfer/export", cancellationToken);

    /// <summary>
    /// The archive with every sealed place opened - its name, description and point put back from the
    /// sealed half, which stays beside them so the file still imports sealed (see ArchivedPlace). The
    /// server cannot do this, having no key, so it is done here with the key LocalPlaceRepository opens
    /// the same places with, and only once the reader has chosen to export places: an archive whose
    /// places were left out asks nothing of the key at all. Orbit.Web's TransferApiClient does the same.
    ///
    /// A place this phone cannot open - no key here, or one sealed under a key pair since replaced - keeps
    /// its empty words and is counted rather than failing the whole export, so the screen can say how
    /// many went into the file unread.
    /// </summary>
    public async Task<OpenedArchive> OpenPlacesAsync(OrbitArchive archive, CancellationToken cancellationToken = default)
    {
        var sealedCount = archive.AllPlaces.Count(IsSealed);
        if (sealedCount == 0)
        {
            return new OpenedArchive(archive, 0);
        }

        PrivateContentKey key;
        try
        {
            key = await _privateContent.UnlockAsync(cancellationToken);
        }
        catch (EncryptionKeyLockedException)
        {
            return new OpenedArchive(archive, sealedCount);
        }

        using (key)
        {
            var places = new List<ArchivedPlace>(archive.AllPlaces.Count);
            var unopened = 0;
            foreach (var place in archive.AllPlaces)
            {
                if (!IsSealed(place))
                {
                    places.Add(place);
                    continue;
                }

                if (Open(key, place.EncryptedContent!) is not { } opened)
                {
                    unopened++;
                    places.Add(place);
                    continue;
                }

                places.Add(place with
                {
                    Name = opened.Name,
                    Description = opened.Description,
                    Where = new ArchivedEventLocation(
                        opened.Where.Address ?? string.Empty, opened.Where.Latitude, opened.Where.Longitude)
                });
            }

            return new OpenedArchive(archive with { Places = places }, unopened);
        }
    }

    private static bool IsSealed(ArchivedPlace place) => place.IsPrivate && place.EncryptedContent is not null;

    private static SealedPlace? Open(PrivateContentKey key, ArchivedEncryptedContent sealedContent)
        => key.Open(
            new EncryptedContentDto(sealedContent.Ciphertext, sealedContent.Nonce),
            SealedContentSerializerContext.Default.SealedPlace);

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

        // A file either client wrote may carry private places opened; they go up closed - see
        // OrbitArchive.WithPrivatePlacesClosed.
        using var response = await _httpClient.PostAsJsonAsync(
            "api/transfer/import", archive.WithPrivatePlacesClosed(), cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ImportArchiveResult>(cancellationToken)
            : null;
    }

    /// <summary>An archive ready to be written, and how many sealed places it carries unopened.</summary>
    public sealed record OpenedArchive(OrbitArchive Archive, int UnopenedPlaces);
}
