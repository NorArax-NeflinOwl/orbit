using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Screens.Navigation;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Sync;

/// <summary>
/// The line in the corner of every screen. What it has to get right is the difference between the app
/// working as designed and something being wrong: offline is the former, refused is the latter, and a
/// reader who cannot tell them apart either ignores real failures or chases imaginary ones.
/// </summary>
public sealed class SyncStateTests
{
    [Fact]
    public void Nothing_is_claimed_before_anything_has_tried()
    {
        var state = Build(isOnline: true);

        Assert.Equal(SyncCondition.Unknown, state.Condition);
        Assert.Equal(string.Empty, new StatusStripViewModel(state).Label);
    }

    [Fact]
    public void A_sync_that_worked_says_so_and_is_dated()
    {
        var state = Build(isOnline: true);

        state.RecordSucceeded();

        Assert.Equal(SyncCondition.Synced, state.Condition);
        Assert.NotNull(state.LastSyncedAtUtc);
        Assert.Equal("Synced", new StatusStripViewModel(state).Label);
    }

    [Fact]
    public void Failing_while_offline_reads_as_offline_rather_than_broken()
    {
        var state = Build(isOnline: false);

        state.RecordFailed();

        Assert.Equal(SyncCondition.Offline, state.Condition);
        Assert.False(new StatusStripViewModel(state).NeedsAttention);
    }

    [Fact]
    public void Failing_while_online_is_worth_a_second_look()
    {
        var state = Build(isOnline: true);

        state.RecordFailed();

        Assert.Equal(SyncCondition.Failed, state.Condition);
        Assert.True(new StatusStripViewModel(state).NeedsAttention);
    }

    [Fact]
    public void The_strip_follows_the_state_it_is_already_watching()
    {
        var state = Build(isOnline: true);
        var strip = new StatusStripViewModel(state);

        state.RecordStarted();

        Assert.True(strip.IsSyncing);
        Assert.Equal("Syncing…", strip.Label);
    }

    [Fact]
    public void A_strip_that_went_away_stops_following()
    {
        // Every page builds one, so a strip that kept its subscription would pile up behind the reader.
        var state = Build(isOnline: true);
        var strip = new StatusStripViewModel(state);
        strip.Dispose();

        state.RecordSucceeded();

        Assert.Equal(string.Empty, strip.Label);
    }

    private static SyncState Build(bool isOnline)
        => new(isOnline ? FixedNetworkStatus.Online : FixedNetworkStatus.Offline,
            new FakeTimeProvider(DateTimeOffset.Parse("2026-08-27T09:00:00Z")));
}
