using System.Globalization;
using Orbit.Core.Location;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// The addresses that ask a map app for directions to a pin. Worth pinning down in a test because every
/// one of them is a third party's own syntax, read once from their documentation and never seen again -
/// a wrong parameter opens the app on nothing at all, which looks exactly like a device with no map app.
/// </summary>
public sealed class NavigationAppsTests
{
    /// <summary>
    /// The whole reason this is C# rather than a line of JavaScript. A Polish decimal comma in a
    /// coordinate is a different place, or no place: this asserts against a thread that would write one.
    /// </summary>
    [Fact]
    public void A_coordinate_is_written_the_same_way_whatever_language_the_reader_is_in()
    {
        var wasCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
        try
        {
            var route = NavigationApps.RouteTo(NavigationApp.GoogleMaps, isApple: false, 52.2297, 21.0122);

            Assert.Contains("52.2297%2C21.0122", route, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = wasCulture;
        }
    }

    /// <summary>
    /// The device's own app is the one answer that differs by platform: iOS handles neither Android's
    /// navigation intent nor geo:, and nothing but an Apple device handles maps://.
    /// </summary>
    [Fact]
    public void The_device_is_asked_in_its_own_scheme()
    {
        Assert.Equal(
            "maps://?daddr=52.2297,21.0122&dirflg=d",
            NavigationApps.RouteTo(NavigationApp.ThisDevice, isApple: true, 52.2297, 21.0122));
        Assert.Equal(
            "google.navigation:q=52.2297,21.0122",
            NavigationApps.RouteTo(NavigationApp.ThisDevice, isApple: false, 52.2297, 21.0122));
    }

    /// <summary>
    /// Every one of these is directions rather than a dropped pin. A pin is what the map already shows;
    /// the whole point of handing over is being taken there.
    /// </summary>
    [Theory]
    [InlineData(NavigationApp.GoogleMaps, "/maps/dir/")]
    [InlineData(NavigationApp.AppleMaps, "daddr=")]
    [InlineData(NavigationApp.Waze, "navigate=yes")]
    [InlineData(NavigationApp.OpenStreetMap, "/directions?")]
    public void Every_app_is_asked_for_a_route_rather_than_a_pin(NavigationApp app, string marker)
    {
        var route = NavigationApps.RouteTo(app, isApple: false, 52.2297, 21.0122);

        Assert.Contains(marker, route, StringComparison.Ordinal);
        Assert.Contains("52.2297", route, StringComparison.Ordinal);
    }

    /// <summary>
    /// Apple Maps is only offered where it is certainly installed. Elsewhere its address is a page that
    /// asks the reader to come back on an iPhone, which is not an option worth putting on a menu.
    /// </summary>
    [Fact]
    public void Apple_maps_is_offered_on_an_apple_device_and_nowhere_else()
    {
        Assert.Contains(NavigationApp.AppleMaps, NavigationApps.Offered(isApple: true));
        Assert.DoesNotContain(NavigationApp.AppleMaps, NavigationApps.Offered(isApple: false));
    }

    /// <summary>
    /// The device's own app is first on both, because it is the only one of them that reaches nobody
    /// else - which is the same reason the map withholds its own tiles until it is allowed them.
    /// </summary>
    [Fact]
    public void The_device_comes_first_wherever_this_is_read()
    {
        Assert.Equal(NavigationApp.ThisDevice, NavigationApps.Offered(isApple: true)[0]);
        Assert.Equal(NavigationApp.ThisDevice, NavigationApps.Offered(isApple: false)[0]);
        Assert.False(NavigationApps.LeavesTheDevice(NavigationApp.ThisDevice));
        Assert.True(NavigationApps.Offered(isApple: false).Skip(1).All(NavigationApps.LeavesTheDevice));
    }
}
