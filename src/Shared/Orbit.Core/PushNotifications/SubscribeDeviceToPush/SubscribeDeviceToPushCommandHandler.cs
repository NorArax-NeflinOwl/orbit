using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;

namespace Orbit.Core.PushNotifications.SubscribeDeviceToPush;

public sealed class SubscribeDeviceToPushCommandHandler : IRequestHandler<SubscribeDeviceToPushCommand, bool>
{
    private readonly IPushSubscriptionRepository _pushSubscriptionRepository;

    public SubscribeDeviceToPushCommandHandler(IPushSubscriptionRepository pushSubscriptionRepository)
    {
        _pushSubscriptionRepository = pushSubscriptionRepository;
    }

    /// <summary>
    /// Always succeeds: FCM hands the app the same token on every launch until it rotates, so
    /// registering repeatedly replaces the stored row rather than accumulating one per launch - see
    /// <see cref="IPushSubscriptionRepository.AddOrReplaceAsync"/>.
    /// </summary>
    public async Task<bool> HandleAsync(SubscribeDeviceToPushCommand request, CancellationToken cancellationToken)
    {
        var subscription = PushSubscription.CreateForDevice(
            request.UserId, new DeviceRegistration(request.DeviceToken, request.Platform));
        await _pushSubscriptionRepository.AddOrReplaceAsync(subscription, cancellationToken);
        return true;
    }
}
