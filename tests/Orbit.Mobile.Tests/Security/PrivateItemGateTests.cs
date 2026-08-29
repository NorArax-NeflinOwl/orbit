using Orbit.Mobile.Security;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Security;

/// <summary>
/// The gate in front of private notes, lists and warehouses.
///
/// IsPrivate already means "only the owner can read this, and the server never can". This is the half
/// of that promise a phone is responsible for: the same guarantee against the person holding it rather
/// than against the server.
/// </summary>
public sealed class PrivateItemGateTests
{
    [Fact]
    public void Private_things_start_locked()
    {
        var context = new GateContext();

        Assert.False(context.Gate.IsUnlocked);
    }

    [Fact]
    public async Task Confirming_who_is_holding_the_phone_unlocks_them()
    {
        var context = new GateContext();

        Assert.True(await context.Gate.TryUnlockAsync());
        Assert.True(context.Gate.IsUnlocked);
    }

    [Fact]
    public async Task A_refusal_leaves_them_locked()
    {
        var context = new GateContext();
        context.Device.Outcome = DeviceAuthenticationOutcome.Refused;

        Assert.False(await context.Gate.TryUnlockAsync());
        Assert.False(context.Gate.IsUnlocked);
    }

    [Fact]
    public async Task A_phone_with_nothing_to_ask_with_stays_locked()
    {
        // Exactly backwards to let somebody in because the phone has no passcode: that is the phone
        // least likely to still be in the hands of its owner.
        var context = new GateContext();
        context.Device.Outcome = DeviceAuthenticationOutcome.NotAvailableOnThisDevice;

        Assert.False(await context.Gate.TryUnlockAsync());
        Assert.False(context.Gate.IsUnlocked);
    }

    [Fact]
    public async Task Unlocking_twice_only_asks_once()
    {
        // Being asked for a face once per note is how a feature becomes something people switch off.
        var context = new GateContext();
        await context.Gate.TryUnlockAsync();

        await context.Gate.TryUnlockAsync();

        Assert.Equal(1, context.Device.TimesAsked);
    }

    [Fact]
    public async Task Putting_the_phone_down_locks_them_again()
    {
        var context = new GateContext();
        await context.Gate.TryUnlockAsync();

        context.Gate.Lock();

        Assert.False(context.Gate.IsUnlocked);
    }

    [Fact]
    public async Task Locking_and_unlocking_tell_the_lists_to_redraw()
    {
        var context = new GateContext();
        var changes = 0;
        context.Gate.Changed += (_, _) => changes++;

        await context.Gate.TryUnlockAsync();
        context.Gate.Lock();

        Assert.Equal(2, changes);
    }

    [Fact]
    public void Locking_something_already_locked_says_nothing()
    {
        // Otherwise every trip to the background would redraw every list for no reason.
        var context = new GateContext();
        var changes = 0;
        context.Gate.Changed += (_, _) => changes++;

        context.Gate.Lock();

        Assert.Equal(0, changes);
    }

    private sealed class GateContext
    {
        public FixedDeviceAuthentication Device { get; } = new();

        public PrivateItemGate Gate { get; }

        public GateContext() => Gate = new PrivateItemGate(Device);
    }
}
