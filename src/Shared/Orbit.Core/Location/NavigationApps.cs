using System.Globalization;

namespace Orbit.Core.Location;

/// <summary>
/// Which app takes somebody to a pin, and the address that asks it to. A map that shows where a thing
/// is and cannot say how to get there answers half the question somebody opened it with.
///
/// Asked rather than guessed, because there is no right answer to guess: one reader has Google Maps and
/// no others, one drives with Waze, one keeps third parties off their phone entirely and wants whatever
/// the device itself registered for a map. So the choice is offered every time, with the device's own
/// app first - it is the only one of them that sends nothing to anybody before it opens.
///
/// The addresses are built here rather than in JavaScript so they can be read back in a test. Only which
/// platform this is comes from the browser (see mapApp.js's isApple), because nothing in .NET running in
/// a browser can tell an iPad from a Mac.
/// </summary>
public enum NavigationApp
{
    /// <summary>Whatever the device registered for a map. Apple's own scheme on an Apple device, Android's navigation intent elsewhere.</summary>
    ThisDevice,
    GoogleMaps,
    AppleMaps,
    Waze,
    OpenStreetMap
}

public static class NavigationApps
{
    /// <summary>
    /// The apps worth offering here, in the order they are drawn. The device's own is first because it
    /// is the one that reaches nobody else; Apple Maps is only offered on an Apple device, where it is
    /// the map that is certainly installed - elsewhere its address is a page that asks the reader to
    /// come back on an iPhone.
    /// </summary>
    public static IReadOnlyList<NavigationApp> Offered(bool isApple)
        => isApple
            ? [NavigationApp.ThisDevice, NavigationApp.AppleMaps, NavigationApp.GoogleMaps, NavigationApp.Waze, NavigationApp.OpenStreetMap]
            : [NavigationApp.ThisDevice, NavigationApp.GoogleMaps, NavigationApp.Waze, NavigationApp.OpenStreetMap];

    /// <summary>
    /// What the app is called on the button, as the English key both clients' dictionaries are keyed by.
    /// Only the first is translated - the rest are the products' own names, which do not change language.
    /// </summary>
    public static string NameOf(NavigationApp app) => app switch
    {
        NavigationApp.ThisDevice => "The app on this device",
        NavigationApp.AppleMaps => "Apple Maps",
        NavigationApp.Waze => "Waze",
        NavigationApp.OpenStreetMap => "OpenStreetMap",
        _ => "Google Maps"
    };

    /// <summary>Whether choosing this one is a request to somebody other than the reader's own device.</summary>
    public static bool LeavesTheDevice(NavigationApp app) => app != NavigationApp.ThisDevice;

    /// <summary>
    /// The address that starts directions to a point. Directions rather than a dropped pin: this is the
    /// answer to "take me there", and every one of these apps has a way of being asked that directly.
    ///
    /// Coordinates are written invariantly, and that is load-bearing: a Polish decimal comma in a
    /// coordinate is a different place, or no place at all.
    /// </summary>
    /// <param name="isApple">
    /// Read from the browser - see mapApp.js. Only <see cref="NavigationApp.ThisDevice"/> uses it: iOS
    /// does not handle Android's navigation intent, and nothing else handles Apple's own scheme.
    /// </param>
    public static string RouteTo(NavigationApp app, bool isApple, double latitude, double longitude)
    {
        var point = string.Create(CultureInfo.InvariantCulture, $"{latitude},{longitude}");
        return app switch
        {
            // Apple's own scheme rather than an https address, for the reason mapApp.js gives: a URL
            // would be a request to Apple before the app ever opened.
            NavigationApp.ThisDevice when isApple => $"maps://?daddr={point}&dirflg=d",
            // Android's navigation intent. geo: only drops a pin, which is the one thing this is not.
            NavigationApp.ThisDevice => $"google.navigation:q={point}",
            NavigationApp.AppleMaps => $"https://maps.apple.com/?daddr={point}&dirflg=d",
            NavigationApp.Waze => $"https://waze.com/ul?ll={point}&navigate=yes",
            // OpenStreetMap's directions take "from;to"; leaving the from empty makes it ask.
            NavigationApp.OpenStreetMap => $"https://www.openstreetmap.org/directions?route=%3B{Uri.EscapeDataString(point)}",
            _ => $"https://www.google.com/maps/dir/?api=1&destination={Uri.EscapeDataString(point)}&travelmode=driving"
        };
    }
}
