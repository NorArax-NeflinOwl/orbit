using Orbit.Contracts;
namespace Orbit.Contracts.Tasks;

/// <summary>
/// IsShared/SharedByUserName/AccessLevel/OriginalOwnerUserId describe provenance, not content - see
/// Orbit.Contracts.Notes.NoteDto's comment for what each means and how the client uses OriginalOwnerUserId.
/// </summary>
public sealed record TaskDto(
    Guid Id, string Title, IReadOnlyList<TaskItemDto> Items, bool IsCompleted, bool IsGroup,
    bool IsPrivate, EncryptedContentDto? EncryptedContent,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    bool IsShared, string? SharedByUserName, string AccessLevel, Guid? OriginalOwnerUserId,
    string Priority = "Normal", string Status = "New");
