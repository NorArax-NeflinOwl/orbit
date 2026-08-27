using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Mobile.Api;
using Orbit.Mobile.Notifications;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Notifications;

/// <summary>
/// Telling Orbit how to reach this phone. The behaviour worth pinning down is what happens when it
/// cannot: push is an addition to the in-app feed, so every failure here has to be survivable, and a
/// device that cannot be delivered to must not be registered as though it could.
/// </summary>
public sealed class PushRegistrationTests
{
    [Fact]
    public async Task A_device_that_agrees_is_registered_with_its_platform()
    {
        var context = new RegistrationContext();
        context.Device.Result = new PushRegistrationResult(PushRegistrationOutcome.Registered, "token-abc");

        var registered = await context.Register().RegisterThisDeviceAsync();

        Assert.True(registered);
        var subscription = Assert.Single(context.Server.RegisteredDevices);
        Assert.Equal("token-abc", subscription.DeviceToken);
        Assert.Equal("Ios", subscription.Platform);
    }

    [Fact]
    public async Task A_reader_who_declined_is_not_registered_and_nothing_fails()
    {
        var context = new RegistrationContext();
        context.Device.Result = new PushRegistrationResult(PushRegistrationOutcome.NotPermitted);

        var registered = await context.Register().RegisterThisDeviceAsync();

        Assert.False(registered);
        Assert.Empty(context.Server.RegisteredDevices);
    }

    [Fact]
    public async Task A_build_that_cannot_produce_a_token_registers_nothing()
    {
        // The state this build is in until an APNs key exists. Registering a placeholder would be worse
        // than registering nothing: the server would count the device as reachable and consider every
        // notification delivered, with no way to observe that none arrived.
        var context = new RegistrationContext();
        context.Device.Result = new PushRegistrationResult(PushRegistrationOutcome.NotAvailableHere);

        var registered = await context.Register().RegisterThisDeviceAsync();

        Assert.False(registered);
        Assert.Empty(context.Server.RegisteredDevices);
    }

    [Fact]
    public async Task An_empty_token_is_treated_as_no_token()
    {
        // Belt and braces against a platform that reports success and hands back nothing.
        var context = new RegistrationContext();
        context.Device.Result = new PushRegistrationResult(PushRegistrationOutcome.Registered, string.Empty);

        Assert.False(await context.Register().RegisterThisDeviceAsync());
        Assert.Empty(context.Server.RegisteredDevices);
    }

    [Fact]
    public async Task Being_offline_at_sign_in_is_survivable()
    {
        var context = new RegistrationContext();
        context.Device.Result = new PushRegistrationResult(PushRegistrationOutcome.Registered, "token-abc");
        context.Server.IsUnreachable = true;

        // No throw: the next sign-in registers again, and the reader is signed in either way.
        Assert.False(await context.Register().RegisterThisDeviceAsync());
    }

    [Fact]
    public async Task A_platform_that_throws_does_not_take_the_sign_in_down()
    {
        var context = new RegistrationContext();
        context.Device.Failure = new InvalidOperationException("no push entitlement in this build");

        Assert.False(await context.Register().RegisterThisDeviceAsync());
        Assert.Empty(context.Server.RegisteredDevices);
    }

    private sealed class RegistrationContext
    {
        public FakeNotificationServer Server { get; } = new();

        public FixedDevicePushNotifications Device { get; } = new();

        public PushRegistration Register()
            => new(Device, new NotificationsClient(Server.ToHttpClient()), NullLogger<PushRegistration>.Instance);
    }
}
