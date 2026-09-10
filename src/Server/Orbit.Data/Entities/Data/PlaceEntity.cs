namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a place - see <see cref="Orbit.Core.Places.Place"/>. UserId is its one true
/// owner for the row's whole life, the same way a note's is; nothing shares a place yet.
/// </summary>
public sealed class PlaceEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Where it is, as three columns rather than one: the address is what a reader searches and reads,
    /// and the pair of numbers is what a map draws. Split the same way a calendar event's place is
    /// (see CalendarEventEntity), because it holds the same sort of thing.
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
