using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Sync;

/// <summary>
/// The timer that keeps the phone in step while the app is open. Every screen synchronises what it shows
/// when it is opened, which is enough for somebody moving about the app and nothing at all for somebody
/// sitting on one screen - which is how a phone came to be days behind a browser.
/// </summary>
public sealed class PeriodicSyncTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-15T09:00:00Z");

    private static readonly UserSession SignedIn =
        new("access", "refresh", Guid.NewGuid(), "me@orbit.example", "Me");

    [Fact]
    public async Task A_run_happens_at_once_rather_than_one_interval_later()
    {
        var context = new SyncingContext();

        context.Sync.Start();
        // Coming back to the app is the moment the screen on display is furthest out of date, so waiting
        // five minutes to do anything about it is the whole problem over again.
        await context.WaitForRunsAsync(1);

        Assert.Equal(1, context.Runs);
    }

    [Fact]
    public async Task And_again_every_interval_after_that()
    {
        var context = new SyncingContext();
        context.Sync.Start();
        await context.WaitForRunsAsync(1);

        context.Clock.Advance(PeriodicSync.Interval);
        await context.WaitForRunsAsync(2);
        context.Clock.Advance(PeriodicSync.Interval);
        await context.WaitForRunsAsync(3);

        Assert.Equal(3, context.Runs);
    }

    [Fact]
    public async Task Stopping_stops_it()
    {
        var context = new SyncingContext();
        context.Sync.Start();
        await context.WaitForRunsAsync(1);

        context.Sync.Stop();
        context.Clock.Advance(PeriodicSync.Interval);
        await context.SettleAsync();

        // A phone in a pocket has nobody to be current for - see App.CreateWindow, which stops this
        // beside the presence heartbeat.
        Assert.Equal(1, context.Runs);
    }

    [Fact]
    public async Task Starting_twice_is_not_two_timers()
    {
        var context = new SyncingContext();

        context.Sync.Start();
        context.Sync.Start();
        // Waited for, then settled past: one run has to have happened, and the second timer this is
        // about would show up as a second one arriving after it.
        await context.WaitForRunsAsync(1);

        Assert.Equal(1, context.Runs);
    }

    [Fact]
    public async Task Nothing_is_attempted_with_nobody_signed_in()
    {
        var context = new SyncingContext(signedIn: false);

        await context.Sync.SynchroniseAsync();

        Assert.Equal(0, context.Runs);
    }

    [Fact]
    public async Task Nothing_is_attempted_while_the_phone_believes_it_is_offline()
    {
        var context = new SyncingContext();
        context.Network.Becomes(false);

        await context.Sync.SynchroniseAsync();

        // Working offline is the app behaving as designed; a run every few minutes would put
        // "couldn't sync" in the corner for somebody doing exactly what Orbit offers.
        Assert.Equal(0, context.Runs);
        Assert.Equal(SyncCondition.Unknown, context.State.Condition);
    }

    /// <summary>
    /// A deployment somebody stopped on purpose is not somewhere to keep knocking - the phone has a
    /// network, and there is nothing at the other end of it that will answer as Orbit. The pause ends
    /// when Orbit answers as itself again, which the presence heartbeat finds out long before this would
    /// (see ServerReachability), so there is nothing lost by leaving off.
    /// </summary>
    [Fact]
    public async Task Nothing_is_attempted_while_the_deployment_says_it_is_paused()
    {
        var context = new SyncingContext();
        context.PauseNotice.Notice = new PauseNotice("Orbit is paused until the month turns.", null);
        await context.CanReachOrbit.RecordAnswerNotFromOrbitAsync(CancellationToken.None);

        await context.Sync.SynchroniseAsync();

        Assert.Equal(0, context.Runs);
    }

    [Fact]
    public async Task A_run_that_brought_something_tells_the_screens()
    {
        var context = new SyncingContext { Result = new SyncResult(0, 3, 0, 0, ReachedTheServer: true) };
        var told = 0;
        context.State.BroughtSomethingNew += (_, _) => told++;

        await context.Sync.SynchroniseAsync();

        Assert.Equal(1, told);
        Assert.Equal(SyncCondition.Synced, context.State.Condition);
    }

    [Fact]
    public async Task A_run_that_changed_nothing_leaves_the_screens_alone()
    {
        var context = new SyncingContext { Result = new SyncResult(0, 0, 0, 0, ReachedTheServer: true) };
        var told = 0;
        context.State.BroughtSomethingNew += (_, _) => told++;

        await context.Sync.SynchroniseAsync();

        // A screen redrawing itself every five minutes for nothing is work nobody asked for, and a list
        // that rebuilds under somebody's finger is worse than one that does not.
        Assert.Equal(0, told);
    }

    [Fact]
    public async Task A_run_that_did_not_get_through_says_so_in_the_corner()
    {
        var context = new SyncingContext { Result = new SyncResult(0, 0, 0, 0, ReachedTheServer: false) };

        await context.Sync.SynchroniseAsync();

        Assert.Equal(SyncCondition.Failed, context.State.Condition);
    }

    [Fact]
    public async Task A_refusal_is_reported_rather_than_thrown()
    {
        var context = new SyncingContext { Fails = new HttpRequestException("Session has expired") };

        // Nothing catches this: it is started without being awaited, from a timer with no screen behind
        // it. AppNavigator watches the session store and moves to sign-in when that is what happened.
        await context.Sync.SynchroniseAsync();

        Assert.Equal(SyncCondition.Failed, context.State.Condition);
    }

    private sealed class SyncingContext
    {
        public SyncingContext(bool signedIn = true)
        {
            Clock = new FakeTimeProvider(Now);
            // The two together, which is what the app registers as its INetworkStatus: a phone on a
            // working network whose deployment has been stopped is not online in any sense this cares
            // about. See ServerReachability.
            CanReachOrbit = Reachability.Over(Network, PauseNotice, Clock);
            State = new SyncState(CanReachOrbit, Clock);
            var sessionStore = new SessionStore(new InMemorySessionStorage(signedIn ? SignedIn : null));
            Sync = new PeriodicSync(
                _ =>
                {
                    Runs++;
                    return Fails is null ? Task.FromResult(Result) : throw Fails;
                },
                sessionStore, CanReachOrbit, State, Clock, NullLogger<PeriodicSync>.Instance);
        }

        public FakeTimeProvider Clock { get; }

        /// <summary>The device's own answer, which a test moves on and off the network.</summary>
        public FixedNetworkStatus Network { get; } = FixedNetworkStatus.Online;

        /// <summary>What the deployment says about itself - a test about a pause writes one here.</summary>
        public FixedPauseNotice PauseNotice { get; } = new();

        /// <inheritdoc cref="ServerReachability"/>
        public ServerReachability CanReachOrbit { get; }

        public SyncState State { get; }

        public PeriodicSync Sync { get; }

        /// <summary>How many runs the timer has asked for - the thing every test here counts.</summary>
        public int Runs { get; private set; }

        /// <summary>What a run answers with. The default is a run that got through and changed nothing.</summary>
        public SyncResult Result { get; init; } = new(0, 0, 0, 0, ReachedTheServer: true);

        /// <summary>What a run throws instead of answering, or null when it answers.</summary>
        public HttpRequestException? Fails { get; init; }

        /// <summary>
        /// Lets the loop get as far as it is going to. The run is started without being awaited - there is
        /// no screen behind a timer to await it - so a test has to give the scheduler its turns back
        /// before it counts anything. A handful of them, because one run is several awaits deep.
        ///
        /// For "nothing should have happened", where there is no count to wait for and the turns are all
        /// that can be given. Where a run <em>is</em> expected, <see cref="WaitForRunsAsync"/> waits for
        /// it instead: eight yields is a guess at how long the scheduler needs, and a machine running the
        /// whole suite in parallel disproves that guess a few times a day.
        /// </summary>
        public async Task SettleAsync()
        {
            for (var turn = 0; turn < 8; turn++)
            {
                await Task.Yield();
            }
        }

        /// <summary>
        /// Waits until the timer has asked for this many runs, or gives up and lets the count speak for
        /// itself. A real wait rather than a fixed number of turns: the run is several awaits deep and
        /// how many turns that takes is the scheduler's business, not the test's.
        ///
        /// Five seconds because it never waits that long - it returns on the turn the count arrives, and
        /// the deadline exists only so a broken loop fails as a wrong count rather than as a hang.
        /// </summary>
        public async Task WaitForRunsAsync(int howMany)
        {
            var giveUpAt = DateTime.UtcNow.AddSeconds(5);
            while (Runs < howMany && DateTime.UtcNow < giveUpAt)
            {
                await Task.Yield();
                await Task.Delay(1);
            }

            // And a few turns past it, so a run that should *not* have happened has had its chance to,
            // and a test asserting an exact count is not merely first past the post.
            await SettleAsync();
        }
    }
}
