namespace Orbit.Mobile.Location;

/// <summary>
/// Hands a point to whatever this phone uses for maps and directions.
///
/// Orbit does not draw a map of the reader's own places on the phone, and this is why it does not need
/// to: getting from a place to directions is the one thing a list of places is for, and every phone
/// already has an app that does it far better than a list screen could. The browser asks the same
/// question a different way - see Orbit.Web's NavigationApps, which offers the choice because a browser
/// has no default to hand off to.
///
/// Behind an interface for the usual reason: it leaves the app. What a test can check is that the screen
/// asked, and with which point.
/// </summary>
public interface IMapHandoff
{
    /// <param name="label">What to call the pin once it is there, so it is not a bare dot on a street.</param>
    Task ShowAsync(double latitude, double longitude, string label, CancellationToken cancellationToken = default);
}
