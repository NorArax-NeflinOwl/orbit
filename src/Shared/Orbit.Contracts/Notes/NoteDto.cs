namespace Orbit.Contracts.Notes;

/// <summary>
/// IsShared/SharedByUserName/AccessLevel/OriginalOwnerUserId describe provenance, not content.
/// AccessLevel is "ReadOnly", "Share", or "CanEdit" (see Orbit.Core.Abstractions.ShareAccessLevel);
/// OriginalOwnerUserId is the id of whoever first created the note, before any sharing. Both are only
/// meaningful when IsShared is true - the Blazor note editor uses OriginalOwnerUserId to exclude that
/// person from the "share with" contact picker when re-sharing a received copy (see
/// ShareNoteCommandHandler's class comment for why sharing back to them is never allowed).
/// </summary>
public sealed record NoteDto(
    Guid Id, string Title, IReadOnlyList<NoteContentLineDto> Content, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    bool IsShared, string? SharedByUserName, string AccessLevel, Guid? OriginalOwnerUserId);
