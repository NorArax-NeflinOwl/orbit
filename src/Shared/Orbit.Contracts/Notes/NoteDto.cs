namespace Orbit.Contracts.Notes;

/// <summary>
/// IsShared/SharedByUserName/AccessLevel describe provenance, not content. AccessLevel is "ReadOnly" or
/// "CanEdit" (see Orbit.Core.Abstractions.ShareAccessLevel) and is only meaningful when IsShared is true.
/// </summary>
public sealed record NoteDto(
    Guid Id, string Title, string Content, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    bool IsShared, string? SharedByUserName, string AccessLevel);
