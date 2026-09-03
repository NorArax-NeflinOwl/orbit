using Orbit.Contracts;

namespace Orbit.Contracts.Inventories;

/// <summary>
/// An inventory as one caller sees it. IsShared/SharedByUserName/AccessLevel describe that caller's own
/// relationship to it rather than anything stored on the row - see Orbit.Core.Inventories.Inventory.
/// LockedByUserName names whoever currently holds the edit lock, and is null when nobody does (or when
/// it's the caller's own lock, which never blocks them). OriginalOwnerUserId is set only when the caller
/// reaches this inventory through a share, so the share panel can keep the owner out of the recipient
/// picker - offering them would always be rejected server-side. Mirrors NoteDto.
/// </summary>
/// <param name="IsSharedWithOthers">
/// The owner's side of sharing: somebody else holds accepted access. Always false when
/// <paramref name="IsShared"/> is true, since that describes the other end of the same relationship.
/// The mobile client needs it to decide what may be edited offline - it cannot hold an edit lock, so
/// anything another person can change is read-only until it is back online (info/orbit-maui-plan.md
/// §5.4). Mirrors NoteDto.
/// </param>
public sealed record InventoryDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool IsShared,
    string? SharedByUserName,
    string AccessLevel,
    string? LockedByUserName,
    Guid? OriginalOwnerUserId,
    /// <summary>Readable only by its owner - Name is empty and everything is inside EncryptedContent.</summary>
    bool IsPrivate = false,
    EncryptedContentDto? EncryptedContent = null,
    bool IsSharedWithOthers = false,
    /// <summary>What it is, under its name. Empty for one nobody described, and for a private one.</summary>
    string Description = "");
