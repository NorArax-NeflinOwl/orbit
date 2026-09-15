using Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Orbit.Core.Notes;

namespace Orbit.Api.Notes.Pictures;

/// <summary>
/// The bytes of a note's pictures as blobs, one per picture, named by id. The account is one of its own
/// with public blob access off (see info/azure-setup.md): nothing here is ever a plain link, every read
/// comes through the API and its access check, and a sealed picture's blob is ciphertext the account
/// could not draw even if it were served.
///
/// The container is made on first use rather than at startup, so an API that starts before the account
/// is reachable still starts - pictures are the one thing that would then fail, not the note.
/// </summary>
public sealed class AzureBlobNotePictureStore : INotePictureStore
{
    private readonly BlobContainerClient _container;
    private bool _containerIsThere;

    public AzureBlobNotePictureStore(IOptions<NotePictureSettings> settings)
    {
        _container = new BlobContainerClient(settings.Value.ConnectionString, settings.Value.Container);
    }

    public async Task<long> WriteAsync(Guid pictureId, Stream content, CancellationToken cancellationToken)
    {
        await EnsureContainerAsync(cancellationToken);
        var blob = _container.GetBlobClient(NameOf(pictureId));
        await blob.UploadAsync(content, overwrite: true, cancellationToken);
        var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
        return properties.Value.ContentLength;
    }

    public async Task<Stream?> OpenAsync(Guid pictureId, CancellationToken cancellationToken)
    {
        try
        {
            var download = await _container.GetBlobClient(NameOf(pictureId)).DownloadStreamingAsync(cancellationToken: cancellationToken);
            return download.Value.Content;
        }
        catch (RequestFailedException failure) when (failure.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(Guid pictureId, CancellationToken cancellationToken)
    {
        await _container.GetBlobClient(NameOf(pictureId)).DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_containerIsThere)
        {
            return;
        }

        await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        _containerIsThere = true;
    }

    private static string NameOf(Guid pictureId) => pictureId.ToString("N");
}
