namespace Orbit.Core.Notes;

/// <summary>
/// A picture kept for a note - the row, not the bytes. The bytes live in an <see cref="INotePictureStore"/>
/// (blob storage on Azure, a directory locally), and this is what the server knows about them: which
/// note they belong to, how many there are, and whether they are sealed.
///
/// Settled with the user on 2026-09-14 (see info/future-plan.md, "Pictures in a note"): the bytes go in
/// blob storage rather than the database; a private note's picture is sealed the way a place is - the
/// blob holds ciphertext and nothing readable sits beside it but its length; and 50 MB is the limit for
/// one note, a total that has to be counted, which is why <see cref="SizeBytes"/> is a readable number
/// even for a sealed picture. That length says roughly how big a sealed picture is; it is the same
/// shape of leak a sealed place accepts, and it is stated here rather than found.
/// </summary>
public sealed class NotePicture
{
    public Guid Id { get; private set; }
    public Guid NoteId { get; private set; }

    /// <summary>The note's owner, whoever uploaded it: a picture is the note's, and a note is its owner's.</summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>How many bytes the store holds for it - the ciphertext's length when sealed.</summary>
    public long SizeBytes { get; private set; }

    /// <summary>What the bytes are, for an ordinary picture. Null when sealed: the type is inside the ciphertext, on the line that names the picture.</summary>
    public string? ContentType { get; private set; }

    /// <summary>Whether the store holds ciphertext only its owner's browser can open - see PrivateContentSealer in Orbit.Web.</summary>
    public bool IsSealed { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private NotePicture(Guid id, Guid noteId, Guid ownerUserId, long sizeBytes, string? contentType, bool isSealed, DateTimeOffset createdAtUtc)
    {
        Id = id;
        NoteId = noteId;
        OwnerUserId = ownerUserId;
        SizeBytes = sizeBytes;
        ContentType = contentType;
        IsSealed = isSealed;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// A picture as it is about to be kept. A sealed one carries no content type - what it is, is sealed
    /// with the line that names it - and an unsealed one must say what it is, so nothing can be served
    /// later as "some bytes".
    /// </summary>
    public static NotePicture Create(Guid id, Guid noteId, Guid ownerUserId, long sizeBytes, string? contentType, bool isSealed)
    {
        if (sizeBytes <= 0)
        {
            throw new InvalidRequestException("A picture has to have something in it.");
        }

        if (!isSealed && string.IsNullOrWhiteSpace(contentType))
        {
            throw new InvalidRequestException("A picture has to say what kind of picture it is.");
        }

        return new NotePicture(
            id, noteId, ownerUserId, sizeBytes, isSealed ? null : contentType!.Trim(), isSealed, DateTimeOffset.UtcNow);
    }

    /// <summary>The row as it was stored - for the repository, which is why every field is taken as given.</summary>
    public static NotePicture Restore(Guid id, Guid noteId, Guid ownerUserId, long sizeBytes, string? contentType, bool isSealed, DateTimeOffset createdAtUtc)
        => new(id, noteId, ownerUserId, sizeBytes, contentType, isSealed, createdAtUtc);
}

/// <summary>
/// How much a note may hold. Two numbers, and they are different questions: one bounds the note, the
/// other bounds one upload - Kestrel's own default for a request body, which is what bounds one picture
/// once each is sent in a request of its own.
/// </summary>
public static class NotePictureLimits
{
    /// <summary>50 MB across every picture on one note - settled with the user on 2026-09-14.</summary>
    public const long MaximumBytesPerNote = 50L * 1024 * 1024;

    /// <summary>The largest one picture may be, which is one request - the default Kestrel already applies.</summary>
    public const long MaximumBytesPerPicture = 30L * 1024 * 1024;
}

/// <summary>
/// One picture, drawn in the flow of a note as a kind of line - see <see cref="NoteContentLine.Picture"/>,
/// and the table beside it, which is the same shape of thing. This names the bytes and says how big
/// they draw; the bytes themselves are fetched by id and, for a sealed note, opened in the browser.
///
/// The content type is here as well as on the row, because for a sealed note this line is <em>inside</em>
/// the ciphertext and the row's is null: this is the only place a sealed picture's kind is written.
/// </summary>
/// <param name="WidthPixels">How wide the picture is, so the note can make room for it before the bytes arrive; 0 when unknown.</param>
public sealed record NotePictureLine(Guid PictureId, string ContentType, int WidthPixels = 0, int HeightPixels = 0);
