using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Sync;

/// <summary>
/// Whether the app is in step with the server. What this has to get right is the difference between the
/// app working as designed and something being wrong: offline is the former, refused is the latter, and
/// a reader who cannot tell them apart either ignores real failures or chases imaginary ones.
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
        => new(isOnline ? FixedNetworkStatus.Online : FixedNetworkStatus.Offline,
            new FakeTimeProvider(DateTimeOffset.Parse("2026-08-27T09:00:00Z")));
}
