using Orbit.Contracts.Tasks;

namespace Orbit.Mobile.Data;

/// <summary>
/// A task list as the phone holds it. Mirrors <see cref="TaskDto"/> plus the same sync bookkeeping
/// <see cref="LocalNote"/> carries, and for the same reasons - screens read these rows, never the API.
///
/// The second entity type on the sync spine, and the one that tests whether the spine generalises: a
/// task list is not a single blob of text but a title plus a list of items, each with a due date and its
/// own notification settings.
/// </summary>
public sealed class LocalTaskList : Orbit.Mobile.Sync.ISharedState
{
    /// <summary>The key on this device, generated here and never changing - see <see cref="LocalNote.LocalId"/>.</summary>
    public Guid LocalId { get; set; }

    /// <summary>The id the server knows this list by. Null until a create has actually been accepted.</summary>
    public Guid? ServerId { get; set; }

    public string Title { get; set; } = string.Empty;

    public IReadOnlyList<TaskItemDto> Items { get; set; } = [];

    public bool IsCompleted { get; set; }

    /// <summary>A list that gathers the lists its items link to, rather than holding work of its own.</summary>
    public bool IsGroup { get; set; }

    /// <summary>
    /// The warehouse this list's work is measured against, when one has been chosen. Kept so the stock
    /// check opens knowing which shelf the question is about, rather than asking the server first.
    /// </summary>
    public Guid? LinkedWarehouseId { get; set; }

    public bool IsPrivate { get; set; }

    /// <summary>The sealed title and items of a private list - carried through untouched.</summary>
    public string? EncryptedCiphertext { get; set; }

    public string? EncryptedNonce { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public bool IsShared { get; set; }

    public string? SharedByUserName { get; set; }

    /// <summary>True when the owner shared this list out and another person can change it.</summary>
    public bool IsSharedWithOthers { get; set; }

    public string AccessLevel { get; set; } = "CanEdit";
    /// <summary>
    /// Whoever created it, before any sharing - meaningful only when this arrived through a share. Kept
    /// so somebody holding it read-only can ask them for more: the request is a chat message, and a
    /// message needs somebody to address it to.
    /// </summary>
    public Guid? OwnerUserId { get; set; }

    public string Priority { get; set; } = "Normal";

    public string Status { get; set; } = "New";

    public bool IsPinned { get; set; }

    /// <summary>When the server last confirmed this row - see <see cref="LocalNote.LastSyncedAtUtc"/>.</summary>
    public DateTimeOffset? LastSyncedAtUtc { get; set; }
}
