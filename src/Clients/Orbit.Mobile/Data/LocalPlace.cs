using System.ComponentModel.DataAnnotations.Schema;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Places;

namespace Orbit.Mobile.Data;

/// <summary>
/// A place as the phone holds it. Mirrors <see cref="PlaceDto"/> plus the bookkeeping the server has no
/// reason to know about - the same shape <see cref="LocalNote"/> takes, and for the same reasons.
///
/// Shorter than a note's by exactly what a place does not have. Nothing about a place is ever sealed, so
/// there is no ciphertext here and no key to unlock; and there is no copy-for-editing, because a place
/// is four lines - somebody refused an offline edit of one has not lost an afternoon's writing, and a
/// second copy of a pin is a second pin nobody asked for.
/// </summary>
public sealed class LocalPlace : Orbit.Mobile.Sync.ISharedState
{
    /// <inheritdoc cref="LocalNote.LocalId"/>
    public Guid LocalId { get; set; }

    /// <inheritdoc cref="LocalNote.ServerId"/>
    public Guid? ServerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>The address as somebody reads it, which can be empty for a point nobody has named.</summary>
    public string Address { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>What colour its pin takes, or empty for whatever a place is drawn in.</summary>
    public string Colour { get; set; } = string.Empty;

    /// <inheritdoc cref="LocalNote.Priority"/>
    public string Priority { get; set; } = "Normal";

    /// <summary>
    /// The lists it belongs to, in order. Stored as JSON in one column, the way a task list's entries
    /// are: it is a list the phone reads whole and never queries into.
    /// </summary>
    public IReadOnlyList<Guid> TaskListIds { get; set; } = [];

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <inheritdoc cref="LocalNote.IsShared"/>
    public bool IsShared { get; set; }

    public string? SharedByUserName { get; set; }

    /// <inheritdoc cref="LocalNote.IsSharedWithOthers"/>
    public bool IsSharedWithOthers { get; set; }

    public string AccessLevel { get; set; } = "CanEdit";

    /// <inheritdoc cref="LocalNote.OwnerUserId"/>
    public Guid? OwnerUserId { get; set; }

    /// <inheritdoc cref="LocalNote.LastSyncedAtUtc"/>
    public DateTimeOffset? LastSyncedAtUtc { get; set; }

    /// <summary>Where it is, in the shape the wire and the map both use. Not a column - the three above are.</summary>
    [NotMapped]
    public EventLocationDto Where => new(Address, Latitude, Longitude);
}
