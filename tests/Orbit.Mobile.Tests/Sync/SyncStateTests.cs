using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Sync;

/// <summary>
/// Whether the app is in step with the server. What this has to get right is the difference between the
/// app working as designed and something being wrong: offline and paused are the former, refused is the
/// latter, and a reader who cannot tell them apart either ignores real failures or chases imaginary ones.
///
/// The words themselves now live in the avatar's menu rather than in a strip on every screen - see
/// NavigationBarViewModel and NavigationBarTests.
/// </summary>
public sealed class SyncStateTests
{
    [Fact]
    public void Nothing_is_claimed_before_anything_has_tried()
    {
        var state = Build(isOnline: true);

        Assert.Equal(SyncCondition.Unknown, state.Condition);
        Assert.Null(state.LastSyncedAtUtc);
    }

    [Fact]
    public void A_sync_that_worked_says_so_and_is_dated()
    {
        var state = Build(isOnline: true);

        state.RecordSucceeded();

        Assert.Equal(SyncCondition.Synced, state.Condition);
        Assert.NotNull(state.LastSyncedAtUtc);
    }

    [Fact]
    public void Failing_while_offline_reads_as_offline_rather_than_broken()
    {
        var state = Build(isOnline: false);

        state.RecordFailed();

        Assert.Equal(SyncCondition.Offline, state.Condition);
    }

    [Fact]
    public void Failing_while_online_is_worth_a_second_look()
    {
        var state = Build(isOnline: true);

        state.RecordFailed();

        Assert.Equal(SyncCondition.Failed, state.Condition);
    }

    /// <summary>
    /// The phone has a network and the sync failed, which reads as "worth a look" - unless the
    /// deployment has said it is paused, in which case nothing is wrong and the corner must not say
    /// there is. The pause is what the cost ceiling does to the server every month; see
    /// ServerReachabilityTests for how the phone comes to know it.
    /// </summary>
    [Fact]
    public async Task Failing_while_the_deployment_is_paused_reads_as_paused_not_broken()
    {
        var pauseNotice = new FixedPauseNotice { Notice = new PauseNotice("Back on the 1st.", null) };
        var reachability = Reachability.Over(FixedNetworkStatus.Online, pauseNotice);
        await reachability.RecordAnswerNotFromOrbitAsync(CancellationToken.None);
        var state = new SyncState(reachability, new FakeTimeProvider());

        state.RecordFailed();

        Assert.Equal(SyncCondition.Paused, state.Condition);
    }

    /// <summary>
    /// Only when it actually changes. Whoever is watching redraws on every one of these, and a sync
    /// that reported "synced" twice in a row would redraw for nothing.
    /// </summary>
    [Fact]
    public void The_same_condition_twice_is_announced_once()
    {
        var state = Build(isOnline: true);
        var announcements = 0;
        state.Changed += (_, _) => announcements++;

        state.RecordSucceeded();
        state.RecordSucceeded();

        Assert.Equal(1, announcements);
    }

    private static SyncState Build(bool isOnline)
        => new(isOnline ? Reachability.Online : Reachability.Offline,
            new FakeTimeProvider(DateTimeOffset.Parse("2026-08-27T09:00:00Z")));
}
