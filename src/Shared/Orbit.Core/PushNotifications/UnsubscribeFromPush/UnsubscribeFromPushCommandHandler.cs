using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;

namespace Orbit.Core.PushNotifications.UnsubscribeFromPush;

public sealed class UnsubscribeFromPushCommandHandler : IRequestHandler<UnsubscribeFromPushCommand, bool>
{
    private readonly IPushSubscriptionRepository _pushSubscriptionRepository;

    public UnsubscribeFromPushCommandHandler(IPushSubscriptionRepository pushSubscriptionRepository)
    {
        _pushSubscriptionRepository = pushSubscriptionRepository;
    }

    /// <summary>Returns false when this user never had a subscription at that endpoint to remove.</summary>
    public Task<bool> HandleAsync(UnsubscribeFromPushCommand request, CancellationToken cancellationToken)
        => _pushSubscriptionRepository.RemoveByEndpointAsync(request.UserId, request.Endpoint, cancellationToken);
}
