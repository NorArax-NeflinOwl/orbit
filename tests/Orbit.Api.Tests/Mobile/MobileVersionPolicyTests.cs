using Orbit.Core.Mobile;
using Xunit;

namespace Orbit.Api.Tests.Mobile;

/// <summary>
/// The forced-update gate is the one mechanism that can take the app away from someone mid-use, so what
/// it refuses - and, more importantly, what it does not refuse - is worth pinning down.
/// </summary>
public sealed class MobileVersionPolicyTests
{
    [Fact]
    public void A_deployment_that_configured_nothing_never_blocks_anyone()
    {
        var policy = MobileVersionPolicy.FromConfiguredValues(minimumSupported: "", latest: "");

        // Including a version that makes no sense at all: with no minimum there is nothing to fail.
        Assert.Equal(MobileVersionVerdict.Supported, policy.Decide("1.0.0"));
        Assert.Equal(MobileVersionVerdict.Supported, policy.Decide("not-a-version"));
        Assert.Equal(MobileVersionVerdict.Supported, policy.Decide(null));
    }

    [Fact]
    public void A_build_older_than_the_minimum_is_stopped()
    {
        var policy = MobileVersionPolicy.FromConfiguredValues(minimumSupported: "1.2.0", latest: "1.5.0");

        Assert.Equal(MobileVersionVerdict.UpdateRequired, policy.Decide("1.1.9"));
    }

    [Fact]
    public void The_minimum_itself_is_supported_rather_than_blocked()
    {
        var policy = MobileVersionPolicy.FromConfiguredValues(minimumSupported: "1.2.0", latest: "1.2.0");

        // Off-by-one here would lock out the oldest build the server actually still supports.
        Assert.Equal(MobileVersionVerdict.Supported, policy.Decide("1.2.0"));
    }

    [Fact]
    public void A_supported_but_outdated_build_is_offered_an_update_rather_than_forced()
    {
        var policy = MobileVersionPolicy.FromConfiguredValues(minimumSupported: "1.2.0", latest: "1.5.0");

        Assert.Equal(MobileVersionVerdict.UpdateAvailable, policy.Decide("1.3.0"));
    }

    [Fact]
    public void A_build_newer_than_the_latest_release_is_left_alone()
    {
        var policy = MobileVersionPolicy.FromConfiguredValues(minimumSupported: "1.2.0", latest: "1.5.0");

        // A tester running ahead of the store shouldn't be nagged to "update" to something older.
        Assert.Equal(MobileVersionVerdict.Supported, policy.Decide("1.6.0"));
    }

    [Fact]
    public void A_version_the_server_cannot_read_is_treated_as_too_old()
    {
        var policy = MobileVersionPolicy.FromConfiguredValues(minimumSupported: "1.2.0", latest: "1.5.0");

        // Once a minimum exists, the server can't establish that an unreadable build is supported, and
        // saying so is safe: the client only blocks on a verdict it actually received.
        Assert.Equal(MobileVersionVerdict.UpdateRequired, policy.Decide("garbage"));
        Assert.Equal(MobileVersionVerdict.UpdateRequired, policy.Decide(null));
    }

    [Fact]
    public void A_misconfigured_minimum_relaxes_the_rule_instead_of_locking_everyone_out()
    {
        var policy = MobileVersionPolicy.FromConfiguredValues(minimumSupported: "oops", latest: "1.5.0");

        // A typo in configuration is the likeliest way this feature ever misfires, and the safe
        // direction to fail is "everyone keeps working", not "nobody can open the app". The latest
        // version is still honoured, so the build is offered an update - it just isn't forced into one.
        Assert.Null(policy.MinimumSupported);
        Assert.NotEqual(MobileVersionVerdict.UpdateRequired, policy.Decide("1.0.0"));
        Assert.Equal(MobileVersionVerdict.UpdateAvailable, policy.Decide("1.0.0"));
    }

    [Fact]
    public void Only_a_latest_version_offers_an_update_without_ever_requiring_one()
    {
        var policy = MobileVersionPolicy.FromConfiguredValues(minimumSupported: "", latest: "2.0.0");

        // Announcing a release and retiring old builds are separate decisions: an outdated build is
        // still told about the new one, but nothing blocks while no minimum is set.
        Assert.Equal(MobileVersionVerdict.UpdateAvailable, policy.Decide("1.0.0"));
        Assert.Equal(MobileVersionVerdict.Supported, policy.Decide("2.0.0"));
        Assert.Equal(MobileVersionVerdict.Supported, policy.Decide("not-a-version"));
    }
}
