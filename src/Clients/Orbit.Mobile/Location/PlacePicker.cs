namespace Orbit.Mobile.Location;

/// <summary>How pointing at a place on the map ended.</summary>
public enum PickedPlaceOutcome
{
    /// <summary>A pin was dropped and confirmed, and its address is the answer.</summary>
    Chosen,

    /// <summary>
    /// The reader backed out. Not an error, and nothing is written back - a stray tap on a map must not
    /// rewrite an address somebody typed, which is why the map asks before answering at all.
    /// </summary>
    Cancelled
}

/// <param name="Address">
/// What the pin turned out to be, in words. Empty for a pin nowhere in particular: a point in a field
/// has coordinates and no address, and the reader is told so rather than handed an empty box.
/// </param>
/// <param name="Latitude">
/// Where the pin actually was. Carried as well as the address because a calendar event stores a point
/// first - see EventLocationDto - and an address alone cannot be put on a map. Null when nothing was
/// picked.
/// </param>
public sealed record PickedPlace(
    PickedPlaceOutcome Outcome, string Address = "", double? Latitude = null, double? Longitude = null)
{
    public static PickedPlace Cancelled { get; } = new(PickedPlaceOutcome.Cancelled);

    public static PickedPlace Chosen(string address, double latitude, double longitude)
        => new(PickedPlaceOutcome.Chosen, address, latitude, longitude);
}

/// <summary>
/// Pointing at a place on a map instead of typing it - the other way to say where something happens,
/// and the one that works when nobody knows what the street is called. Orbit.Web opens the same thing
/// over its task editor (see LocationPickerOverlay).
///
/// Behind an interface for the usual reason: it opens a map over the screen, waits for a tap, turns
/// that into an address and asks whether to use it, none of which a test can do. What a test can check
/// is what the screen does with each answer.
/// </summary>
public interface IPlacePicker
{
    /// <param name="startingAddress">
    /// What the box already holds, so the map opens where the reader was talking about rather than in
    /// the middle of the ocean. Ignored when it is not a place that can be found.
    /// </param>
    Task<PickedPlace> PickAsync(string startingAddress, CancellationToken cancellationToken = default);
}
