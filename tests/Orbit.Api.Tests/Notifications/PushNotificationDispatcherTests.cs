using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Notifications;

public sealed class PushNotificationDispatcherTests
{
    private static readonly PushNotificationPayload SamplePayload = new("Title", "Body", "/somewhere");

    [Fact]
    public async Task NotifyUserAsync_sends_to_every_subscription_the_user_has()
    {
        var userId = Guid.NewGuid();
        var subscriptionRepository = new InMemoryPushSubscriptionRepository();
        await subscriptionRepository.AddOrReplaceAsync(
            PushSubscription.CreateForBrowser(userId, new WebPushRegistration("https://push.example/a", "p256dh-a", "auth-a")), CancellationToken.None);
        await subscriptionRepository.AddOrReplaceAsync(
            PushSubscription.CreateForBrowser(userId, new WebPushRegistration("https://push.example/b", "p256dh-b", "auth-b")), CancellationToken.None);
        var sender = new RecordingPushNotificationSender();
        var dispatcher = new PushNotificationDispatcher(subscriptionRepository, [sender], NullLogger<PushNotificationDispatcher>.Instance);

        await dispatcher.NotifyUserAsync(userId, SamplePayload, CancellationToken.None);

        Assert.Equal(2, sender.SentNotifications.Count);
        Assert.All(sender.SentNotifications, sent => Assert.Equal(SamplePayload, sent.Payload));
    }

    [Fact]
    public async Task NotifyUserAsync_does_nothing_when_the_user_has_no_subscriptions()
    {
        var subscriptionRepository = new InMemoryPushSubscriptionRepository();
        var sender = new RecordingPushNotificationSender();
        var dispatcher = new PushNotificationDispatcher(subscriptionRepository, [sender], NullLogger<PushNotificationDispatcher>.Instance);

        await dispatcher.NotifyUserAsync(Guid.NewGuid(), SamplePayload, CancellationToken.None);

        Assert.Empty(sender.SentNotifications);
    }

    [Fact]
    public async Task NotifyUserAsync_prunes_a_subscription_the_push_service_reports_as_expired()
    {
        var userId = Guid.NewGuid();
        var subscriptionRepository = new InMemoryPushSubscriptionRepository();
        var expiredSubscription = PushSubscription.CreateForBrowser(userId, new WebPushRegistration("https://push.example/expired", "p256dh", "auth"));
        await subscriptionRepository.AddOrReplaceAsync(expiredSubscription, CancellationToken.None);
        var sender = new RecordingPushNotificationSender();
        sender.ExpiredSubscriptionIds.Add(expiredSubscription.Id);
        var dispatcher = new PushNotificationDispatcher(subscriptionRepository, [sender], NullLogger<PushNotificationDispatcher>.Instance);

        await dispatcher.NotifyUserAsync(userId, SamplePayload, CancellationToken.None);

        Assert.Empty(sender.SentNotifications);
        Assert.Empty(await subscriptionRepository.GetForUserAsync(userId, CancellationToken.None));
    }

    [Fact]
    public async Task NotifyUserAsync_still_notifies_the_remaining_subscriptions_when_one_delivery_fails()
    {
        var userId = Guid.NewGuid();
        var subscriptionRepository = new InMemoryPushSubscriptionRepository();
        var expiredSubscription = PushSubscription.CreateForBrowser(userId, new WebPushRegistration("https://push.example/expired", "p256dh", "auth"));
        var workingSubscription = PushSubscription.CreateForBrowser(userId, new WebPushRegistration("https://push.example/working", "p256dh", "auth"));
        await subscriptionRepository.AddOrReplaceAsync(expiredSubscription, CancellationToken.None);
        await subscriptionRepository.AddOrReplaceAsync(workingSubscription, CancellationToken.None);
        var sender = new RecordingPushNotificationSender();
        sender.ExpiredSubscriptionIds.Add(expiredSubscription.Id);
        var dispatcher = new PushNotificationDispatcher(subscriptionRepository, [sender], NullLogger<PushNotificationDispatcher>.Instance);

        await dispatcher.NotifyUserAsync(userId, SamplePayload, CancellationToken.None);

        var sent = Assert.Single(sender.SentNotifications);
        Assert.Equal(workingSubscription.Id, sent.SubscriptionId);
    }
}
