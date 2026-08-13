namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a note, mapped separately from <see cref="Orbit.Core.Notes.Note"/> so schema
/// changes don't force changes onto domain logic, and vice versa.
/// </summary>
public sealed class NoteEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
