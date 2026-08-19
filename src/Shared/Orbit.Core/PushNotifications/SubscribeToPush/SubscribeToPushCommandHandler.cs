using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;

namespace Orbit.Core.PushNotifications.SubscribeToPush;

public sealed class SubscribeToPushCommandHandler : IRequestHandler<SubscribeToPushCommand, bool>
{
    private readonly IPushSubscriptionRepository _pushSubscriptionRepository;

    public SubscribeToPushCommandHandler(IPushSubscriptionRepository pushSubscriptionRepository)
    {
        _pushSubscriptionRepository = pushSubscriptionRepository;
    }

    /// <summary>
    /// Always succeeds: registering the same endpoint twice (e.g. the browser re-subscribing on a later
    /// page load) just replaces the stored keys rather than failing - see
    /// <see cref="IPushSubscriptionRepository.AddOrReplaceAsync"/>.
    /// </summary>
    public async Task<bool> HandleAsync(SubscribeToPushCommand request, CancellationToken cancellationToken)
    {
        var subscription = PushSubscription.Create(request.UserId, request.Endpoint, request.P256dhBase64, request.AuthBase64);
        await _pushSubscriptionRepository.AddOrReplaceAsync(subscription, cancellationToken);
        return true;
    }
}
