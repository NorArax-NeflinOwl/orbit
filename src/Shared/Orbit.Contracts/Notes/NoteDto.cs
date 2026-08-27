using Orbit.Contracts;
namespace Orbit.Contracts.Notes;

/// <summary>
/// IsShared/SharedByUserName/AccessLevel/OriginalOwnerUserId describe provenance, not content.
/// AccessLevel is "ReadOnly", "Share", or "CanEdit" (see Orbit.Core.Abstractions.ShareAccessLevel);
/// OriginalOwnerUserId is the id of whoever first created the note, before any sharing. Both are only
/// meaningful when IsShared is true - the Blazor note editor uses OriginalOwnerUserId to exclude that
/// person from the "share with" contact picker when re-sharing a received copy (see
/// ShareNoteCommandHandler's class comment for why sharing back to them is never allowed).
/// </summary>
/// <param name="IsPinned">Sorts this note above the others, on every client that shows a list of them.</param>
/// <param name="IsSharedWithOthers">
/// The owner's side of sharing: somebody else holds accepted access to this note. Always false when
/// <paramref name="IsShared"/> is true, since that describes the other end of the same relationship.
/// The mobile client needs it to decide what may be edited offline - it cannot hold an edit lock, so
/// anything another person can change is read-only until it is back online (info/orbit-maui-plan.md
/// §5.4). Without this an owner's copy of a shared note looks exactly like a private one.
/// </param>
public sealed record NoteDto(
    Guid Id, string Title, IReadOnlyList<NoteContentLineDto> Content, bool IsPrivate, EncryptedContentDto? EncryptedContent,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    bool IsShared, string? SharedByUserName, string AccessLevel, Guid? OriginalOwnerUserId,
    bool IsSharedWithOthers = false, bool IsPinned = false);
