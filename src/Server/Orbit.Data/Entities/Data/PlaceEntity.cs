namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a place - see <see cref="Orbit.Core.Places.Place"/>. UserId is its one true
/// owner for the row's whole life, the same way a note's is - sharing grants access to this row rather
/// than making a copy.
/// </summary>
public sealed class PlaceEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    /// <summary>Empty for a sealed place - its real name is inside <see cref="EncryptedCiphertext"/>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Empty for a sealed place, like the name and the point.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether this place is sealed - and it is unless its owner said otherwise, which is the opposite
    /// default from every other kind of thing here. See Orbit.Core.Places.Place.IsPrivate.
    /// </summary>
    public bool IsPrivate { get; set; }

    /// <summary>The sealed name, description and point of a private place - see SealedPlace.</summary>
    public string? EncryptedCiphertext { get; set; }

    public string? EncryptedNonce { get; set; }

    /// <summary>
    /// Where it is, as three columns rather than one: the address is what a reader searches and reads,
    /// and the pair of numbers is what a map draws. Split the same way a calendar event's place is
    /// (see CalendarEventEntity), because it holds the same sort of thing.
    ///
    /// All three are empty for a sealed place. A place whose coordinates were still readable would be
    /// sealed in name only.
    /// </summary>
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>What colour its pin takes, or empty for whatever a place is drawn in.</summary>
    public string Colour { get; set; } = string.Empty;

    /// <summary>Stored by name, like every other enum here - see Orbit.Core.Abstractions.ItemPriority.</summary>
    public string Priority { get; set; } = nameof(Orbit.Core.Abstractions.ItemPriority.Normal);

    /// <summary>The task lists it belongs to, owned by the place and deleted with it.</summary>
    public List<PlaceTaskListLinkEntity> TaskLists { get; set; } = [];

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
