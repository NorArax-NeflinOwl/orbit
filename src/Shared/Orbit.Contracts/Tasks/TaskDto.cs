using Orbit.Contracts;
namespace Orbit.Contracts.Tasks;

/// <summary>
/// IsShared/SharedByUserName/AccessLevel/OriginalOwnerUserId describe provenance, not content - see
/// Orbit.Contracts.Notes.NoteDto's comment for what each means and how the client uses OriginalOwnerUserId.
/// </summary>
/// <param name="IsSharedWithOthers">
/// The owner's side of sharing: somebody else holds accepted access. Always false when
/// <paramref name="IsShared"/> is true, since that describes the other end of the same relationship.
/// The mobile client needs it to decide what may be edited offline - it cannot hold an edit lock, so
/// anything another person can change is read-only until it is back online (info/orbit-maui-plan.md
/// §5.4). Mirrors NoteDto.
/// </param>
public sealed record TaskDto(
    Guid Id, string Title, IReadOnlyList<TaskItemDto> Items, bool IsCompleted, bool IsGroup,
    bool IsPrivate, EncryptedContentDto? EncryptedContent,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    bool IsShared, string? SharedByUserName, string AccessLevel, Guid? OriginalOwnerUserId,
    string Priority = "Normal", string Status = "New", bool IsPinned = false, bool IsSharedWithOthers = false);
