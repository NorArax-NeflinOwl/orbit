namespace Orbit.Data.Entities;

/// <summary>See Orbit.Core.Notes.NotePicture - the row; the bytes are the picture store's.</summary>
public sealed class NotePictureEntity
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public Guid OwnerUserId { get; set; }

    /// <summary>Readable even for a sealed picture - it is what the note's 50 MB is counted against.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Null when sealed: the kind is inside the ciphertext, on the line that names the picture.</summary>
    public string? ContentType { get; set; }

    public bool IsSealed { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
