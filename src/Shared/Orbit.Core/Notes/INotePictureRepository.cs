namespace Orbit.Core.Notes;

/// <summary>The rows that say which pictures a note keeps - see <see cref="NotePicture"/>. The bytes are the store's.</summary>
public interface INotePictureRepository
{
    Task AddAsync(NotePicture picture, CancellationToken cancellationToken);

    Task<NotePicture?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<NotePicture>> GetForNoteAsync(Guid noteId, CancellationToken cancellationToken);

    /// <summary>
    /// Every picture this account owns, whichever note it is on - what an account deleting itself has to
    /// be able to ask, because the bytes live outside the database and nothing else would ever name them
    /// again. See NotePictureSweeper.RemoveEverythingOwnedByAsync.
    /// </summary>
    Task<IReadOnlyList<NotePicture>> GetForOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken);

    /// <summary>How many bytes a note's pictures take together - what the 50 MB is counted against.</summary>
    Task<long> TotalBytesForNoteAsync(Guid noteId, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Where a picture's bytes are kept, by the id of its row. Blob storage on Azure and a directory
/// locally, behind one interface so nothing above it knows which - see AzureBlobNotePictureStore and
/// DirectoryNotePictureStore in Orbit.Api.
/// </summary>
public interface INotePictureStore
{
    /// <summary>Keeps the bytes, and answers how many there were - what the row records, rather than what a header claimed.</summary>
    Task<long> WriteAsync(Guid pictureId, Stream content, CancellationToken cancellationToken);

    /// <summary>The bytes, to be read once and disposed - or null when the store has nothing under that id.</summary>
    Task<Stream?> OpenAsync(Guid pictureId, CancellationToken cancellationToken);

    /// <summary>Takes the bytes away; nothing to take away is not an error, since a row can outlive a failed write.</summary>
    Task DeleteAsync(Guid pictureId, CancellationToken cancellationToken);
}
