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
    string Description = "",
    /// <summary>
    /// The folder its owner filed it under, or null for one filed nowhere - which is Public, or Private
    /// when it is sealed (Orbit.Core.Folders.BuiltInFolder). Always null for somebody reading this
    /// through a share, the way NoteDto.FolderId is.
    /// </summary>
    Guid? FolderId = null,
    /// <summary>
    /// Whether its owner has put it away - see Orbit.Core.Folders.BuiltInFolder.Archived. False is what
    /// everything stored before the column existed is, and what a server that has not learned about
    /// archiving answers. Defaulted and last, so an older client reads past it.
    /// </summary>
    bool IsArchived = false,
    /// <summary>
    /// The shelves this one gathers, in order - see Orbit.Core.Inventories.Inventory.GathersInventoryIds.
    /// Empty for an ordinary shelf, which is every one stored before gathering existed. Defaulted and
    /// last, so an older client reads past it.
    /// </summary>
    IReadOnlyList<Guid>? GathersInventoryIds = null)
{
    /// <summary>Whichever way the sender said it, read as a list - null means "gathers nothing".</summary>
    public IReadOnlyList<Guid> AllGathered => GathersInventoryIds ?? [];

    /// <summary>Whether this shelf gathers others - see Orbit.Core.Inventories.Inventory.IsGroup.</summary>
    public bool IsGroup => AllGathered.Count > 0;
}
