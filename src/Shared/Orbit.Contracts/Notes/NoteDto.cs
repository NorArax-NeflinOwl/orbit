namespace Orbit.Contracts.Notes;

public sealed record NoteDto(Guid Id, string Title, string Content, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
