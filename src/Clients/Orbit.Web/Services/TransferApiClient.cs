using System.Net.Http.Json;
using Orbit.Contracts;
using Orbit.Contracts.Places;
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
    private readonly PrivateContentSealer? _privateContentSealer;

    public TransferApiClient(HttpClient httpClient, PrivateContentSealer? privateContentSealer = null)
    {
        _httpClient = httpClient;
        _privateContentSealer = privateContentSealer;
    }

    public async Task<OrbitArchive?> ExportAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<OrbitArchive>("api/transfer/export", cancellationToken);

    /// <summary>
    /// The archive with every sealed place opened - its name, description and point put back from the
    /// sealed half, which stays beside them so the file still imports sealed (see ArchivedPlace). The
    /// server cannot do this, having no key, so it is done here, and only once the reader has chosen to
    /// export places: an archive whose places were left out asks nothing of the key at all.
    ///
    /// A place this browser cannot open keeps its empty words and is counted rather than failing the
    /// whole export - the same answer a map with one unreadable pin gives - so the page can say how many
    /// went into the file unread.
    /// </summary>
    public async Task<OpenedArchive> OpenPlacesAsync(OrbitArchive archive, CancellationToken cancellationToken = default)
    {
        var places = new List<ArchivedPlace>(archive.AllPlaces.Count);
        var unopened = 0;
        foreach (var place in archive.AllPlaces)
        {
            if (!place.IsPrivate || place.EncryptedContent is not { } sealedContent)
            {
                places.Add(place);
                continue;
            }

            var opened = await OpenAsync(sealedContent, cancellationToken);
            if (opened is null)
            {
                unopened++;
                places.Add(place);
                continue;
            }

            places.Add(place with
            {
                Name = opened.Name,
                Description = opened.Description,
                Where = new ArchivedEventLocation(opened.Where.Address ?? string.Empty, opened.Where.Latitude, opened.Where.Longitude)
            });
        }

        return new OpenedArchive(archive with { Places = places }, unopened);
    }

    private Task<SealedPlace?> OpenAsync(ArchivedEncryptedContent sealedContent, CancellationToken cancellationToken)
        => _privateContentSealer is null
            ? throw new InvalidOperationException(
                "This TransferApiClient was built without a PrivateContentSealer, so it can't open a private place.")
            : _privateContentSealer.OpenAsync<SealedPlace>(
                new EncryptedContentDto(sealedContent.Ciphertext, sealedContent.Nonce), cancellationToken);

    /// <summary>
    /// Returns null when the server refused the file - a version it doesn't know, or content it can't
    /// make sense of - which the caller shows as "this file couldn't be read" rather than a stack trace.
    ///
    /// A private place goes up closed, whatever the file says about it: a file of opened places is what
    /// an export of them is, but the server was never meant to read one - see
    /// OrbitArchive.WithPrivatePlacesClosed.
    /// </summary>
    public async Task<ImportArchiveResult?> ImportAsync(OrbitArchive archive, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/transfer/import", archive.WithPrivatePlacesClosed(), cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ImportArchiveResult>(cancellationToken: cancellationToken)
            : null;
    }

    /// <summary>An archive ready to be written, and how many sealed places it carries unopened.</summary>
    public sealed record OpenedArchive(OrbitArchive Archive, int UnopenedPlaces);
}
