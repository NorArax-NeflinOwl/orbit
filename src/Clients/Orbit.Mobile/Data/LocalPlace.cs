using System.ComponentModel.DataAnnotations.Schema;
using Orbit.Contracts;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Places;

namespace Orbit.Mobile.Data;

/// <summary>
/// A place as the phone holds it. Mirrors <see cref="PlaceDto"/> plus the bookkeeping the server has no
/// reason to know about - the same shape <see cref="LocalNote"/> takes, and for the same reasons.
///
/// Shorter than a note's by what a place does not have: there is no copy-for-editing, because a place is
/// four lines - somebody refused an offline edit of one has not lost an afternoon's writing, and a second
/// copy of a pin is a second pin nobody asked for.
///
/// What it does have that a note only sometimes does is a sealed half, and it has it by default: a place
/// is private unless its owner said otherwise (see Orbit.Core.Places.Place.IsPrivate). A database file
/// lifted off the handset therefore says no more about where somebody goes than Orbit.Api can.
/// </summary>
public sealed class LocalPlace : Orbit.Mobile.Sync.ISharedState
{
    /// <inheritdoc cref="LocalNote.LocalId"/>
    public Guid LocalId { get; set; }

    /// <inheritdoc cref="LocalNote.ServerId"/>
    public Guid? ServerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The address as somebody reads it, which can be empty for a point nobody has named - and always is
    /// for a sealed place, whose address is inside <see cref="EncryptedContent"/> with the rest.
    /// </summary>
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

    /// <inheritdoc cref="Orbit.Core.Places.Place.IsPrivate"/>
    public bool IsPrivate { get; set; } = true;

    /// <summary>
    /// The sealed name, description and point of a private place. This is where they live: the readable
    /// columns above are empty for one, on the phone exactly as on the server - see LocalNote, which
    /// says the same about a private note.
    /// </summary>
    public string? EncryptedCiphertext { get; set; }

    public string? EncryptedNonce { get; set; }

    /// <inheritdoc cref="LocalNote.EncryptedContent"/>
    [NotMapped]
    public EncryptedContentDto? EncryptedContent
        => EncryptedCiphertext is { } ciphertext && EncryptedNonce is { } nonce
            ? new EncryptedContentDto(ciphertext, nonce)
            : null;

    /// <inheritdoc cref="LocalNote.IsSealed"/>
    [NotMapped]
    public bool IsSealed { get; set; }

    /// <summary>Where it is, in the shape the wire and the map both use. Not a column - the three above are.</summary>
    [NotMapped]
    public EventLocationDto Where => new(Address, Latitude, Longitude);
}
