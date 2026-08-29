using Orbit.Core.Notifications;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IPushNotificationSender"/> stub that records every call instead of sending
/// anything, so tests can assert on what would have been sent (or that nothing was) - the push
/// counterpart of <see cref="RecordingEmailSender"/>. Subscriptions listed in
/// <see cref="ExpiredSubscriptionIds"/> make <see cref="SendAsync"/> throw
/// <see cref="PushSubscriptionExpiredException"/> instead of recording, so
/// PushNotificationDispatcherTests can exercise the pruning path.
/// </summary>
internal sealed class RecordingPushNotificationSender : IPushNotificationSender
{
    public RecordingPushNotificationSender(PushTransport transport = PushTransport.WebPush) => Transport = transport;

    public PushTransport Transport { get; }

    private readonly List<SentPushNotification> _sentNotifications = [];

    public IReadOnlyList<SentPushNotification> SentNotifications => _sentNotifications;

    public HashSet<Guid> ExpiredSubscriptionIds { get; } = [];

    public Task SendAsync(PushSubscription subscription, PushNotificationPayload payload, CancellationToken cancellationToken)
    {
        if (ExpiredSubscriptionIds.Contains(subscription.Id))
        {
            throw new PushSubscriptionExpiredException($"Subscription {subscription.Id} is expired.");
        }

        _sentNotifications.Add(new SentPushNotification(subscription.Id, payload));
        return Task.CompletedTask;
    }

    internal sealed record SentPushNotification(Guid SubscriptionId, PushNotificationPayload Payload);
}
