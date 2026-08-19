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
        _subscriptions.RemoveAll(existing => existing.Endpoint == subscription.Endpoint);
        _subscriptions.Add(subscription);
        return Task.CompletedTask;
    }

    public Task<bool> RemoveByEndpointAsync(Guid userId, string endpoint, CancellationToken cancellationToken)
        => Task.FromResult(_subscriptions.RemoveAll(
            subscription => subscription.UserId == userId && subscription.Endpoint == endpoint) > 0);

    public Task RemoveAsync(Guid subscriptionId, CancellationToken cancellationToken)
    {
        _subscriptions.RemoveAll(subscription => subscription.Id == subscriptionId);
        return Task.CompletedTask;
    }
}
