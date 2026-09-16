using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Sync;

/// <summary>
/// A phone on a working network whose server has been stopped is not online in any sense a screen cares
/// about. These pin down how the app comes to know that - an answer that was not Orbit's, then the
/// pause notice - and, just as important, how it stops knowing it: Orbit answering as itself.
/// </summary>
public sealed class ServerReachabilityTests
{
    private static readonly PauseNotice Paused = new("Back on the 1st.", DateTimeOffset.Parse("2026-09-20T17:00:00Z"));

    [Fact]
    public void With_a_network_and_no_pause_the_phone_is_online()
    {
        var reachability = Reachability.Over(FixedNetworkStatus.Online);

        Assert.True(reachability.IsOnline);
        Assert.True(reachability.HasNetwork);
        Assert.False(reachability.IsPaused);
    }

    [Fact]
    public void Without_a_network_the_phone_is_offline_whatever_the_deployment_says()
    {
        var reachability = Reachability.Over(FixedNetworkStatus.Offline);

        Assert.False(reachability.IsOnline);
        Assert.False(reachability.HasNetwork);
    }

    /// <summary>
    /// The defining case: the network is fine, the address answered, and it was not Orbit. Once the
    /// notice says the deployment is paused, every screen asking INetworkStatus is told "offline" - and
    /// the two halves are kept apart, so the corner can still say which one it is.
    /// </summary>
    [Fact]
    public async Task An_answer_not_from_Orbit_with_a_pause_notice_takes_the_phone_offline_in_all_but_network()
    {
        var pauseNotice = new FixedPauseNotice { Notice = Paused };
        var reachability = Reachability.Over(FixedNetworkStatus.Online, pauseNotice);
        var announced = 0;
        reachability.Changed += (_, _) => announced++;

        await reachability.RecordAnswerNotFromOrbitAsync(CancellationToken.None);

        Assert.False(reachability.IsOnline);
        Assert.True(reachability.HasNetwork);
        Assert.True(reachability.IsPaused);
        Assert.Equal(Paused, reachability.Notice);
        Assert.Equal(1, announced);
    }

    /// <summary>
    /// Not every wrong answer is a pause - a proxy, a captive portal, a stop that forgot to write the
    /// file. With no notice the phone stays online and lets the failure be what it is; the outbox and
    /// the refresh are protected by the exception's shape (see AnswerNotFromOrbitException), not by this.
    /// </summary>
    [Fact]
    public async Task An_answer_not_from_Orbit_with_no_notice_changes_nothing()
    {
        var reachability = Reachability.Over(FixedNetworkStatus.Online, new FixedPauseNotice());
        var announced = 0;
        reachability.Changed += (_, _) => announced++;

        await reachability.RecordAnswerNotFromOrbitAsync(CancellationToken.None);

        Assert.True(reachability.IsOnline);
        Assert.False(reachability.IsPaused);
        Assert.Equal(0, announced);
    }

    [Fact]
    public async Task Orbit_answering_ends_the_pause_on_the_spot()
    {
        var pauseNotice = new FixedPauseNotice { Notice = Paused };
        var reachability = Reachability.Over(FixedNetworkStatus.Online, pauseNotice);
        await reachability.RecordAnswerNotFromOrbitAsync(CancellationToken.None);
        var announced = 0;
        reachability.Changed += (_, _) => announced++;

        reachability.RecordAnswerFromOrbit();

        Assert.True(reachability.IsOnline);
        Assert.Null(reachability.Notice);
        Assert.Equal(1, announced);
    }

    [Fact]
    public void Orbit_answering_when_nothing_was_paused_announces_nothing()
    {
        var reachability = Reachability.Over(FixedNetworkStatus.Online);
        var announced = 0;
        reachability.Changed += (_, _) => announced++;

        reachability.RecordAnswerFromOrbit();

        Assert.Equal(0, announced);
    }

    /// <summary>
    /// A paused day is a failed request per sync, and each one must not cost a read of the notice too:
    /// the notice is re-read no more often than the recheck interval, and the pause is believed in
    /// between. Past the interval it is read again, which is how a resume nobody asked the API about
    /// first is noticed within the hour.
    /// </summary>
    [Fact]
    public async Task While_paused_the_notice_is_read_again_only_after_the_recheck_interval()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-09-20T17:00:00Z"));
        var pauseNotice = new FixedPauseNotice { Notice = Paused };
        var reachability = Reachability.Over(FixedNetworkStatus.Online, pauseNotice, clock);
        await reachability.RecordAnswerNotFromOrbitAsync(CancellationToken.None);

        clock.Advance(ServerReachability.RecheckInterval - TimeSpan.FromMinutes(1));
        await reachability.RecordAnswerNotFromOrbitAsync(CancellationToken.None);
        Assert.Equal(1, pauseNotice.Reads);
        Assert.True(reachability.IsPaused);

        pauseNotice.Notice = null;
        clock.Advance(TimeSpan.FromMinutes(1));
        await reachability.RecordAnswerNotFromOrbitAsync(CancellationToken.None);
        Assert.Equal(2, pauseNotice.Reads);
        Assert.False(reachability.IsPaused);
    }

    /// <summary>The device's own changes are passed on: a screen greying out on connectivity still hears about it.</summary>
    [Fact]
    public void The_network_going_away_is_announced_through_the_reachability()
    {
        var network = FixedNetworkStatus.Online;
        var reachability = Reachability.Over(network);
        var announced = 0;
        reachability.Changed += (_, _) => announced++;

        network.Becomes(false);

        Assert.Equal(1, announced);
        Assert.False(reachability.IsOnline);
    }
}
