using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Holding a shared item while an editor is open on it - what the phone never did, and Orbit.Web has
/// always done. The cost of not doing it was quiet: an edit made while somebody else held the lock came
/// back refused at sync time and was abandoned with only a log line to show for it.
/// </summary>
public sealed class EditLockTests
{
    private static readonly Guid Item = Guid.NewGuid();

    [Fact]
    public async Task An_item_nobody_else_has_is_taken()
    {
        var items = new CountingItems();
        var editLock = Locking(out _);

        Assert.True(await editLock.HoldAsync(items, Item));
        Assert.False(editLock.IsHeldByAnother);
        Assert.Equal(1, items.Acquisitions);
    }

    [Fact]
    public async Task An_item_somebody_else_has_says_who_has_it()
    {
        var items = new CountingItems { HeldBy = "Ala" };
        var editLock = Locking(out _);

        Assert.False(await editLock.HoldAsync(items, Item));
        Assert.True(editLock.IsHeldByAnother);
        Assert.Equal("Ala", editLock.HeldByOtherUserName);
        Assert.Contains("Ala", editLock.RefusalMessage);
    }

    /// <summary>
    /// The server drops the claim after a minute, so it is renewed while the editor stays open. An
    /// editor left open for five minutes must not silently stop holding anything.
    /// </summary>
    [Fact]
    public async Task The_claim_is_renewed_while_the_editor_stays_open()
    {
        var items = new CountingItems();
        var editLock = Locking(out var clock);
        await editLock.HoldAsync(items, Item);

        // A tick at a time, the way it actually happens: each renewal re-arms the timer, so a single
        // jump of a minute produces one, not three.
        foreach (var beat in Enumerable.Range(2, 3))
        {
            var renewed = items.WaitFor(beat);
            clock.Advance(TimeSpan.FromSeconds(25));
            Assert.True(await renewed, $"Renewed {items.Acquisitions} times, expected {beat}.");
        }
    }

    [Fact]
    public async Task Releasing_lets_go_and_stops_renewing()
    {
        var items = new CountingItems();
        var editLock = Locking(out var clock);
        await editLock.HoldAsync(items, Item);

        await editLock.ReleaseAsync();
        var renewalsAtRelease = items.Acquisitions;
        var renewedAgain = items.WaitFor(renewalsAtRelease + 1);
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.False(await renewedAgain);

        Assert.Equal(1, items.Releases);
        Assert.Equal(renewalsAtRelease, items.Acquisitions);
    }

    /// <summary>
    /// Only possible when the phone was away long enough for the claim to lapse. The editor has to be
    /// told, because by then somebody else's work is the one that will survive.
    /// </summary>
    [Fact]
    public async Task Losing_it_mid_edit_is_announced()
    {
        var items = new CountingItems();
        var editLock = Locking(out var clock);
        await editLock.HoldAsync(items, Item);

        // Waited on directly rather than through the renewal that causes it: the renewal is counted
        // before the announcement is made, so waiting on the count would race the thing being tested.
        var announced = new TaskCompletionSource();
        editLock.Changed += (_, _) => announced.TrySetResult();

        items.HeldBy = "Ola";
        clock.Advance(TimeSpan.FromSeconds(25));

        Assert.Same(announced.Task, await Task.WhenAny(announced.Task, Task.Delay(TimeSpan.FromSeconds(2))));
        Assert.Equal("Ola", editLock.HeldByOtherUserName);
    }

    /// <summary>
    /// A claim nobody could ask about must not shut the reader out of their own editor. Offline is not
    /// this class's problem - OfflineEditPolicy decides it from what the phone already knows.
    /// </summary>
    [Fact]
    public async Task Offline_it_asks_nobody_and_blocks_nobody()
    {
        var items = new CountingItems();
        var editLock = new EditLock(FixedNetworkStatus.Offline, new FakeTimeProvider(), Translations());

        Assert.True(await editLock.HoldAsync(items, Item));
        Assert.Equal(0, items.Acquisitions);
    }

    [Fact]
    public async Task A_server_that_cannot_be_reached_blocks_nobody()
    {
        var items = new CountingItems { Unreachable = true };
        var editLock = Locking(out _);

        Assert.True(await editLock.HoldAsync(items, Item));
        Assert.False(editLock.IsHeldByAnother);
    }

    /// <summary>Opening a second item lets the first go, rather than holding both.</summary>
    [Fact]
    public async Task Holding_something_else_lets_the_first_one_go()
    {
        var items = new CountingItems();
        var editLock = Locking(out _);

        await editLock.HoldAsync(items, Item);
        await editLock.HoldAsync(items, Guid.NewGuid());

        Assert.Equal(1, items.Releases);
    }

    private static EditLock Locking(out FakeTimeProvider clock)
    {
        clock = new FakeTimeProvider();
        return new EditLock(FixedNetworkStatus.Online, clock, Translations());
    }

    private static Translations Translations() => new(new InMemoryLanguageStore());

    /// <summary>
    /// The heartbeat runs on its own, so a test that advances the clock and asserts on the next line
    /// races it. WaitFor is how a test says "once this many have happened" instead of hoping.
    /// </summary>
    private sealed class CountingItems : ILockableItems
    {
        private static readonly TimeSpan LongEnoughToBeSure = TimeSpan.FromSeconds(2);

        private Expectation? _expected;

        public int Acquisitions { get; private set; }

        public int Releases { get; private set; }

        public string? HeldBy { get; set; }

        public bool Unreachable { get; set; }

        public Task<EditClaim> AcquireLockAsync(Guid serverId, CancellationToken cancellationToken = default)
        {
            if (Unreachable)
            {
                throw new HttpRequestException("No route to the server.");
            }

            Acquisitions++;
            if (_expected is { } expected && Acquisitions >= expected.Acquisitions)
            {
                expected.Arrived.TrySetResult();
            }

            return Task.FromResult(new EditClaim(HeldBy));
        }

        public Task ReleaseLockAsync(Guid serverId, CancellationToken cancellationToken = default)
        {
            Releases++;
            return Task.CompletedTask;
        }

        /// <summary>False when they never came, which is what a test asserting on silence wants.</summary>
        public async Task<bool> WaitFor(int acquisitions)
        {
            if (Acquisitions >= acquisitions)
            {
                return true;
            }

            var expected = new Expectation(acquisitions, new TaskCompletionSource());
            _expected = expected;
            var finished = await Task.WhenAny(expected.Arrived.Task, Task.Delay(LongEnoughToBeSure));
            return finished == expected.Arrived.Task;
        }

        private sealed record Expectation(int Acquisitions, TaskCompletionSource Arrived);
    }
}
