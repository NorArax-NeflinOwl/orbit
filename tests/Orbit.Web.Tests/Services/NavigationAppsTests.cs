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
    /// The device's own app is not offered, although it used to come first. What answers these two
    /// addresses is a scheme rather than a page, and a browser with nothing registered for one does
    /// nothing and says nothing - a button that looks broken rather than one that reaches nobody. So
    /// everything on the list leaves the device, and the panel says so once instead of per button.
    /// Taken off on 2026-09-19; RouteTo still answers for it.
    /// </summary>
    [Fact]
    public void The_device_is_not_among_the_apps_offered()
    {
        Assert.DoesNotContain(NavigationApp.ThisDevice, NavigationApps.Offered(isApple: true));
        Assert.DoesNotContain(NavigationApp.ThisDevice, NavigationApps.Offered(isApple: false));
        Assert.True(NavigationApps.Offered(isApple: true).All(NavigationApps.LeavesTheDevice));
        Assert.True(NavigationApps.Offered(isApple: false).All(NavigationApps.LeavesTheDevice));
    }
}
