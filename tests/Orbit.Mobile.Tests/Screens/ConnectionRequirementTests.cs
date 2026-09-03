using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// What the screens ask before offering something that only works with a connection.
///
/// The bug this exists to stop: a handful of actions need a verdict only the server can give, and they
/// used to say so *after* being tapped. Somebody typed a new password, pressed the button, and only
/// then learned it was never going to work.
/// </summary>
public sealed class ConnectionRequirementTests
{
    [Fact]
    public void An_online_phone_can_be_offered_everything()
    {
        var requirement = Connections.Online;

        Assert.True(requirement.IsMet);
        Assert.False(requirement.IsNotMet);
    }

    [Fact]
    public void An_offline_phone_is_told_why_rather_than_left_to_find_out()
    {
        var requirement = Connections.Offline;

        Assert.False(requirement.IsMet);
        Assert.True(requirement.IsNotMet);
        Assert.NotEmpty(requirement.Explanation);
    }

    /// <summary>
    /// The half that is easy to forget: a button left dead after the connection came back is as wrong as
    /// one offered while it could only fail, and nobody thinks to leave the screen and come back.
    /// </summary>
    [Fact]
    public void A_phone_that_finds_a_network_stops_holding_things_back()
    {
        var network = FixedNetworkStatus.Offline;
        var requirement = Connections.For(network);
        var announced = 0;
        requirement.PropertyChanged += (_, _) => announced++;

        network.Becomes(true);

        Assert.True(requirement.IsMet);
        Assert.True(announced > 0, "The screen was never told, so whatever it disabled stays disabled.");
    }

    /// <summary>And the other way, for a phone that walks out of range with a form open.</summary>
    [Fact]
    public void A_phone_that_loses_the_network_starts_holding_things_back()
    {
        var network = FixedNetworkStatus.Online;
        var requirement = Connections.For(network);

        network.Becomes(false);

        Assert.True(requirement.IsNotMet);
    }
}
