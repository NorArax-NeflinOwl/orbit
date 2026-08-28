using Orbit.Contracts.Notes;

namespace Orbit.Mobile.Data;

/// <summary>
/// A note as the phone holds it. Mirrors <see cref="NoteDto"/> plus the bookkeeping the server has no
/// reason to know about - see info/orbit-maui-plan.md §5.1.
///
/// Screens read these rows, never the API directly. That is what makes the app work offline, and it is
/// structural rather than an optimisation: a screen written against the API cannot be given offline
/// support later without rewriting it.
/// </summary>
public sealed class LocalNote : Orbit.Mobile.Sync.ISharedState
{
    /// <summary>
    /// The key on this device, generated here and never changing. Distinct from <see cref="ServerId"/>
    /// because a note created offline exists before the server has ever seen it, and rows that already
    /// point at it must not have to be found and rewritten once it does.
    /// </summary>
    public Guid LocalId { get; set; }

    /// <summary>The id the server knows this note by. Null until a create has actually been accepted.</summary>
    public Guid? ServerId { get; set; }

    public string Title { get; set; } = string.Empty;

    public IReadOnlyList<NoteContentLineDto> Content { get; set; } = [];

    public bool IsPrivate { get; set; }

    /// <summary>The sealed title and lines of a private note - carried through untouched; the phone cannot open it yet.</summary>
    public string? EncryptedCiphertext { get; set; }

    public string? EncryptedNonce { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>True when this note reached the phone through somebody else's share.</summary>
    public bool IsShared { get; set; }

    public string? SharedByUserName { get; set; }

    /// <summary>True when the owner shared this note out and another person can change it.</summary>
    public bool IsSharedWithOthers { get; set; }

    public string AccessLevel { get; set; } = "CanEdit";

    /// <summary>
    /// When the server last confirmed this row. Null for a note created offline that has never been
    /// accepted - which is also what <see cref="ServerId"/> being null means, kept separately because a
    /// synced note that is later edited offline has one and not the other.
    /// </summary>
    public DateTimeOffset? LastSyncedAtUtc { get; set; }
}
