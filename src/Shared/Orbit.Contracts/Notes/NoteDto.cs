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
    bool IsSharedWithOthers = false,
    bool IsPinned = false,
    /// <summary>ItemPriority by name - "Low", "Normal" or "High".</summary>
    string Priority = "Normal",
    /// <summary>
    /// The folder its owner filed it under, or null for one filed nowhere - which is a built-in folder
    /// rather than none at all, see Orbit.Core.Folders.BuiltInFolder. Only ever the owner's own filing:
    /// a note shared with somebody else carries the owner's folder id, which means nothing to them.
    /// </summary>
    Guid? FolderId = null,
    /// <summary>
    /// The words it is tagged with - see Orbit.Core.Notes.Note.Tags. Empty for a private note, whose tags
    /// are in its sealed payload (SealedNote.Tags). Null from a server written before tags existed.
    /// </summary>
    IReadOnlyList<string>? Tags = null)
{
    /// <summary>The tags as something to read without a null check - see <see cref="Tags"/>.</summary>
    public IReadOnlyList<string> AllTags => Tags ?? [];
}
