namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of <see cref="Orbit.Core.Notes.NoteShare"/>, mapped separately so schema changes
/// don't force changes onto domain logic, and vice versa. Mirrors CalendarEventShareEntity.
/// </summary>
public sealed class NoteShareEntity
{
    public Guid Id { get; set; }
    public Guid SourceNoteId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid RecipientUserId { get; set; }
    public string AccessLevel { get; set; } = "ReadOnly";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
}
