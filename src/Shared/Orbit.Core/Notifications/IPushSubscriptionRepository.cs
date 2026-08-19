namespace Orbit.Core.Notifications;

/// <summary>
/// Stores which browsers have approved push notifications for which user, and their Web Push
/// credentials - see <see cref="PushSubscription"/>.
/// </summary>
public interface IPushSubscriptionRepository
{
    Task<IReadOnlyList<PushSubscription>> GetForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts <paramref name="subscription"/>, or replaces the existing row for the same
    /// <see cref="PushSubscription.Endpoint"/> if one already exists - a browser that (re-)subscribes
    /// keeps the same endpoint, so this is what makes registering idempotent instead of accumulating
    /// duplicate rows on every page load.
    /// </summary>
    Task AddOrReplaceAsync(PushSubscription subscription, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the subscription for <paramref name="userId"/> at <paramref name="endpoint"/>, if any -
    /// used when the user explicitly turns push notifications off in their browser.
    /// </summary>
    Task<bool> RemoveByEndpointAsync(Guid userId, string endpoint, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a subscription by its own id - used by <see cref="PushNotificationDispatcher"/> to prune
    /// a subscription the push service has reported as permanently gone (see
    /// <see cref="PushSubscriptionExpiredException"/>), regardless of which user it belonged to.
    /// </summary>
    Task RemoveAsync(Guid subscriptionId, CancellationToken cancellationToken);
}
