using Microsoft.Extensions.Logging;

namespace Orbit.Core.Notifications;

/// <summary>
/// Fans a single notification out to every push subscription a user currently has (they may have
/// approved push notifications in more than one browser or device) - the shared entry point
/// CalendarEventReminderBackgroundService, SendMessageCommandHandler and
/// OverdueTaskNotificationBackgroundService in Orbit.Api all call, so "who has push enabled and how to
/// reach them" lives in exactly one place.
///
/// Never throws: a failed or expired delivery is logged (and, when expired, the stale subscription is
/// pruned) here rather than surfaced to the caller, since none of those callers' own work - sending an
/// email, saving a chat message, polling for overdue tasks - should fail just because a push
/// notification could not be delivered.
/// </summary>
public sealed class PushNotificationDispatcher
{
    private readonly IPushSubscriptionRepository _pushSubscriptionRepository;
    private readonly IPushNotificationSender _pushNotificationSender;
    private readonly ILogger<PushNotificationDispatcher> _logger;

    public PushNotificationDispatcher(
        IPushSubscriptionRepository pushSubscriptionRepository,
        IPushNotificationSender pushNotificationSender,
        ILogger<PushNotificationDispatcher> logger)
    {
        _pushSubscriptionRepository = pushSubscriptionRepository;
        _pushNotificationSender = pushNotificationSender;
        _logger = logger;
    }

    public async Task NotifyUserAsync(Guid userId, PushNotificationPayload payload, CancellationToken cancellationToken)
    {
        var subscriptions = await _pushSubscriptionRepository.GetForUserAsync(userId, cancellationToken);
        foreach (var subscription in subscriptions)
        {
            try
            {
                await _pushNotificationSender.SendAsync(subscription, payload, cancellationToken);
            }
            catch (PushSubscriptionExpiredException)
            {
                await _pushSubscriptionRepository.RemoveAsync(subscription.Id, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception, "Failed to deliver a push notification to subscription {SubscriptionId} for user {UserId}",
                    subscription.Id, userId);
            }
        }
    }
}
