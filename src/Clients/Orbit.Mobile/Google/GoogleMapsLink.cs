using System.Globalization;
using System.Web;

namespace Orbit.Mobile.Google;

/// <summary>
/// Builds Google Maps links for a place and for directions to it. Plain URLs against Google's
/// documented "Maps URLs" endpoints - no API key, no quota, and they open in whatever the phone treats
/// as Maps, app or browser.
///
/// The phone's own twin of Orbit.Web's GoogleMapsLink, the way the chat crypto is twinned: both clients
/// build the same URLs from the same contracts, and the tests on each side are what keep them saying the
/// same thing.
/// </summary>
public static class GoogleMapsLink
{
    /// <summary>
    /// Points at coordinates. Coordinates rather than the address alone because an address string can be
    /// ambiguous or fail to geocode, while a pair of coordinates always lands exactly where the point was
    /// taken.
    /// </summary>
    public static string ToPlace(double latitude, double longitude)
        => $"https://www.google.com/maps/search/?api=1&query={Format(latitude)},{Format(longitude)}";

    /// <summary>For a place known only by name or address - what a typed-in location gives.</summary>
    public static string ToPlace(string address)
        => $"https://www.google.com/maps/search/?api=1&query={HttpUtility.UrlEncode(address)}";

    /// <summary>
    /// Directions to a destination, deliberately without an origin. Google then routes from where the
    /// reader actually is when they open the link. Passing Orbit's recorded position instead would look
    /// more precise and be worse: that point is whatever they last recorded on purpose, so a route could
    /// start from another city they were in days ago.
    /// </summary>
    public static string ToDirections(double destinationLatitude, double destinationLongitude)
        => $"https://www.google.com/maps/dir/?api=1&destination={Format(destinationLatitude)},{Format(destinationLongitude)}";

    /// <summary>Invariant culture on purpose: a decimal comma would split the coordinate pair in two.</summary>
    private static string Format(double coordinate) => coordinate.ToString("G", CultureInfo.InvariantCulture);
}
