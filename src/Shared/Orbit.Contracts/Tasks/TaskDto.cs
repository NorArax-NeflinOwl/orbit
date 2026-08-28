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
    string Priority = "Normal", string Status = "New", bool IsPinned = false,
    /// <summary>The warehouse this list's work is measured against, when one has been chosen - see the stock check.</summary>
    Guid? LinkedWarehouseId = null,
    /// <summary>What the list is for - "Checklist" or "Calendar", see Orbit.Core.Tasks.TaskListKind.</summary>
    string Kind = "Checklist",
    /// <summary>Where a calendar list happens; empty for every other kind.</summary>
    string Location = "");
