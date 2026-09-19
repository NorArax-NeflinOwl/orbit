namespace Orbit.Contracts.Location;

/// <summary>
/// Where a link to somebody else's map points - see Orbit.Core.Location.MapLinks, which reads it, and
/// the endpoint under /api/location/map-link, which is asked only about the links a client cannot read
/// for itself.
/// </summary>
/// <param name="Latitude">Null together with <paramref name="Longitude"/> for a link that named words rather than a point.</param>
/// <param name="Search">What the link asked to be found, where it carried no coordinates.</param>
/// <param name="Label">What the link calls the place, where it says.</param>
/// <param name="Url">
/// The link as it was written, not the address it turned out to stand for: that is what "open the
/// original" has to open, and a shortener's answer is not what anybody wrote down.
/// </param>
public sealed record ResolvedMapLinkDto(
    double? Latitude, double? Longitude, string? Search, string? Label, string Url);
