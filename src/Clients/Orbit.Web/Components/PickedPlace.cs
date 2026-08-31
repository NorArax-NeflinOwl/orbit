namespace Orbit.Web.Components;

/// <summary>
/// A place somebody confirmed on the map: where it is, and what to call it.
///
/// The two travel together because neither is the whole answer. The words are what a reader recognises
/// and is asked to confirm; the coordinates are what a calendar event needs in order to be a place at
/// all rather than a label (see <see cref="Orbit.Core.Calendar.EventLocation"/>). Handing back only the
/// address is what made a confirmed pin arrive as nothing.
/// </summary>
public sealed record PickedPlace(string Address, double Latitude, double Longitude);
