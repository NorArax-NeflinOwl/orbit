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
    private readonly IReadOnlyDictionary<PushTransport, IPushNotificationSender> _sendersByTransport;
    private readonly ILogger<PushNotificationDispatcher> _logger;

    public PushNotificationDispatcher(
        IPushSubscriptionRepository pushSubscriptionRepository,
        IEnumerable<IPushNotificationSender> pushNotificationSenders,
        ILogger<PushNotificationDispatcher> logger)
    {
        _pushSubscriptionRepository = pushSubscriptionRepository;
        // Last registration for a transport wins, so a deployment can substitute one without also
        // having to remove the default.
        _sendersByTransport = pushNotificationSenders
            .GroupBy(sender => sender.Transport)
            .ToDictionary(group => group.Key, group => group.Last());
        _logger = logger;
    }

    public async Task NotifyUserAsync(Guid userId, PushNotificationPayload payload, CancellationToken cancellationToken)
    {
        var subscriptions = await _pushSubscriptionRepository.GetForUserAsync(userId, cancellationToken);
        foreach (var subscription in subscriptions)
        {
            if (!_sendersByTransport.TryGetValue(subscription.Transport, out var sender))
            {
                // A subscription of a transport this deployment has no sender for - e.g. a phone
                // registered against a build that has since dropped Firebase. Not an error worth
                // shouting about, and certainly not worth failing the caller's own work over.
                _logger.LogDebug(
                    "No push sender configured for {Transport}; skipping subscription {SubscriptionId}",
                    subscription.Transport, subscription.Id);
                continue;
            }

            try
            {
                await sender.SendAsync(subscription, payload, cancellationToken);
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
