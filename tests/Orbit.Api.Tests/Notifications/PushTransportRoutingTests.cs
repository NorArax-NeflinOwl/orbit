using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Mobile;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Notifications;

/// <summary>
/// A browser and a phone are reached in entirely different ways, so the dispatcher has to pick the
/// right sender for each. Sending a Web Push message to a phone would not merely fail - it would fail
/// silently against the wrong service - so these pin down that each subscription reaches exactly one
/// sender, and that an unroutable one is skipped rather than taking the caller's work down with it.
/// </summary>
public sealed class PushTransportRoutingTests
{
    private static readonly PushNotificationPayload Payload = new("Title", "Body", "/notes");

    [Fact]
    public async Task Each_subscription_goes_to_the_sender_for_its_own_transport()
    {
        var userId = Guid.NewGuid();
        var context = new RoutingContext(userId, browsers: 1, devices: 1);

        await context.NotifyAsync(userId);

        Assert.Single(context.Browser.SentNotifications);
        Assert.Single(context.Firebase.SentNotifications);
        // Each sender saw exactly the subscription belonging to its own transport, not the other's.
        var stored = await context.Repository.GetForUserAsync(userId, CancellationToken.None);
        var browserId = stored.Single(subscription => subscription.Transport == PushTransport.WebPush).Id;
        var deviceId = stored.Single(subscription => subscription.Transport == PushTransport.Firebase).Id;
        Assert.Equal(browserId, context.Browser.SentNotifications.Single().SubscriptionId);
        Assert.Equal(deviceId, context.Firebase.SentNotifications.Single().SubscriptionId);
    }

    [Fact]
    public async Task A_user_with_only_browsers_never_reaches_the_mobile_sender()
    {
        var userId = Guid.NewGuid();
        var context = new RoutingContext(userId, browsers: 2, devices: 0);

        await context.NotifyAsync(userId);

        Assert.Equal(2, context.Browser.SentNotifications.Count);
        Assert.Empty(context.Firebase.SentNotifications);
    }

    [Fact]
    public async Task A_subscription_with_no_sender_configured_is_skipped_without_failing_the_rest()
    {
        // A phone registered against a deployment that has since dropped Firebase. The browser beside
        // it must still be notified - the caller's own work (saving a message, sending an email)
        // must not fail because one destination is unreachable.
        var userId = Guid.NewGuid();
        var repository = new InMemoryPushSubscriptionRepository();
        await repository.AddOrReplaceAsync(
            PushSubscription.CreateForBrowser(userId, new WebPushRegistration("https://push.example/a", "k", "a")),
            CancellationToken.None);
        await repository.AddOrReplaceAsync(
            PushSubscription.CreateForDevice(userId, new DeviceRegistration("device-token", MobilePlatform.Android)),
            CancellationToken.None);

        var browser = new RecordingPushNotificationSender(PushTransport.WebPush);
        var dispatcher = new PushNotificationDispatcher(
            repository, [browser], NullLogger<PushNotificationDispatcher>.Instance);

        await dispatcher.NotifyUserAsync(userId, Payload, CancellationToken.None);

        Assert.Single(browser.SentNotifications);
    }

    [Fact]
    public async Task An_expired_device_token_prunes_that_subscription_and_leaves_the_browser_alone()
    {
        var userId = Guid.NewGuid();
        var repository = new InMemoryPushSubscriptionRepository();
        await repository.AddOrReplaceAsync(
            PushSubscription.CreateForBrowser(userId, new WebPushRegistration("https://push.example/a", "k", "a")),
            CancellationToken.None);
        await repository.AddOrReplaceAsync(
            PushSubscription.CreateForDevice(userId, new DeviceRegistration("dead-token", MobilePlatform.Ios)),
            CancellationToken.None);

        var browser = new RecordingPushNotificationSender(PushTransport.WebPush);
        var firebase = new ExpiringPushNotificationSender(PushTransport.Firebase);
        var dispatcher = new PushNotificationDispatcher(
            repository, [browser, firebase], NullLogger<PushNotificationDispatcher>.Instance);

        await dispatcher.NotifyUserAsync(userId, Payload, CancellationToken.None);

        // An app that was uninstalled should stop being tried; the browser is unaffected.
        var remaining = await repository.GetForUserAsync(userId, CancellationToken.None);
        Assert.Equal(PushTransport.WebPush, Assert.Single(remaining).Transport);
        Assert.Single(browser.SentNotifications);
    }

    private sealed class RoutingContext
    {
        public InMemoryPushSubscriptionRepository Repository { get; } = new();
        public RecordingPushNotificationSender Browser { get; } = new(PushTransport.WebPush);
        public RecordingPushNotificationSender Firebase { get; } = new(PushTransport.Firebase);

        public RoutingContext(Guid userId, int browsers, int devices)
        {
            for (var index = 0; index < browsers; index++)
            {
                Repository.AddOrReplaceAsync(
                    PushSubscription.CreateForBrowser(userId, new WebPushRegistration($"https://push.example/{index}", "k", "a")),
                    CancellationToken.None).GetAwaiter().GetResult();
            }

            for (var index = 0; index < devices; index++)
            {
                Repository.AddOrReplaceAsync(
                    PushSubscription.CreateForDevice(userId, new DeviceRegistration($"token-{index}", MobilePlatform.Ios)),
                    CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        public Task NotifyAsync(Guid userId)
            => new PushNotificationDispatcher(
                Repository, [Browser, Firebase], NullLogger<PushNotificationDispatcher>.Instance)
                .NotifyUserAsync(userId, Payload, CancellationToken.None);
    }

    private sealed class ExpiringPushNotificationSender(PushTransport transport) : IPushNotificationSender
    {
        public PushTransport Transport { get; } = transport;

        public Task SendAsync(PushSubscription subscription, PushNotificationPayload payload, CancellationToken cancellationToken)
            => throw new PushSubscriptionExpiredException("gone");
    }
}
