namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of <see cref="Orbit.Core.Places.PlaceShare"/>, mapped separately so schema changes
/// do not force changes onto domain logic and back. Mirrors NoteShareEntity, minus the recipient's pin:
/// a place has no list of its own to sit at the top of.
/// </summary>
public sealed class PlaceShareEntity
{
    public Guid Id { get; set; }
    public Guid SourcePlaceId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid RecipientUserId { get; set; }
    public string AccessLevel { get; set; } = "ReadOnly";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
}
