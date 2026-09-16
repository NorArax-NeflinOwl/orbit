using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// A route drawn on the map, stops and all, handed to Google Maps - see GoogleMapsLink.ForRoute. The stops
/// were asked for on the list of 2026-09-16; the link is how the route reaches the phone that drives it.
/// </summary>
public sealed class GoogleMapsLinkRouteTests
{
    [Fact]
    public void A_route_with_stops_carries_them_as_waypoints_in_order()
    {
        var link = GoogleMapsLink.ForRoute(52.1, 21.5, [(52.2, 21.25), (52.3, 21)], 52.4, 20.75);

        Assert.Equal(
            "https://www.google.com/maps/dir/?api=1&origin=52.1,21.5&destination=52.4,20.75"
                + "&waypoints=52.2%2C21.25%7C52.3%2C21",
            link);
    }

    [Fact]
    public void A_route_without_stops_has_no_waypoints()
    {
        Assert.DoesNotContain("waypoints", GoogleMapsLink.ForRoute(52.1, 21.5, [], 52.4, 20.75));
    }
}