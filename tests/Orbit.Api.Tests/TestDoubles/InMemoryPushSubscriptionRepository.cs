using Orbit.Core.Notifications;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IPushSubscriptionRepository"/> stub for unit tests, standing in for the
/// SQLite-backed storage PushSubscriptionRepository provides.
/// </summary>
internal sealed class InMemoryPushSubscriptionRepository : IPushSubscriptionRepository
{
    private readonly List<PushSubscription> _subscriptions = [];

    public Task<IReadOnlyList<PushSubscription>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<PushSubscription>>(
            _subscriptions.Where(subscription => subscription.UserId == userId).ToList());

    public Task AddOrReplaceAsync(PushSubscription subscription, CancellationToken cancellationToken)
    {
        // Matched the way the real repository matches: on whichever value identifies the destination,
        // since a device subscription has no endpoint and a browser one has no device token.
        _subscriptions.RemoveAll(existing =>
            (subscription.WebPush is { } webPush && existing.WebPush?.Endpoint == webPush.Endpoint)
            || (subscription.Device is { } device && existing.Device?.Token == device.Token));
        _subscriptions.Add(subscription);
        return Task.CompletedTask;
    }

    public Task<bool> RemoveByEndpointAsync(Guid userId, string endpoint, CancellationToken cancellationToken)
        => Task.FromResult(_subscriptions.RemoveAll(
            subscription => subscription.UserId == userId && subscription.WebPush?.Endpoint == endpoint) > 0);

    public Task RemoveAsync(Guid subscriptionId, CancellationToken cancellationToken)
    {
        _subscriptions.RemoveAll(subscription => subscription.Id == subscriptionId);
        return Task.CompletedTask;
    }
}
