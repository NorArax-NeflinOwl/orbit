using Microsoft.AspNetCore.SignalR;
using Orbit.Core.LiveUpdates;

namespace Orbit.Api.LiveUpdates;

/// <summary>
/// Delivers Orbit.Core's announcements down whatever connections are open, over SignalR - named for the
/// transport the way VapidPushNotificationSender and FirebasePushNotificationSender are.
///
/// Every method here swallows its own failures. Announcing is not the work; it is the shortcut that
/// saves the client from asking. The message is already written down and the client already knows how to
/// find it, so an announcement that cannot be delivered must never fail the request that caused it - a
/// sent message should not come back as an error because a WebSocket was in the middle of reconnecting.
/// </summary>
public sealed class SignalRLiveUpdatePublisher(
    IHubContext<LiveUpdatesHub> hub,
    ILogger<SignalRLiveUpdatePublisher> logger) : ILiveUpdatePublisher
{
    public Task ChatChangedAsync(Guid userId, CancellationToken cancellationToken)
        => AnnounceAsync([userId], LiveUpdateMessages.ChatChanged, cancellationToken);

    public Task ChatChangedAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
        => AnnounceAsync(userIds, LiveUpdateMessages.ChatChanged, cancellationToken);

    public Task NotificationsChangedAsync(Guid userId, CancellationToken cancellationToken)
        => AnnounceAsync([userId], LiveUpdateMessages.NotificationsChanged, cancellationToken);

    public Task PresenceChangedAsync(
        Guid userId, IReadOnlyCollection<Guid> visibleToUserIds, CancellationToken cancellationToken)
        => AnnounceAsync(visibleToUserIds, LiveUpdateMessages.PresenceChanged, cancellationToken, userId);

    private async Task AnnounceAsync(
        IReadOnlyCollection<Guid> userIds, string message, CancellationToken cancellationToken, params object[] arguments)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        try
        {
            // Addressed by account rather than by connection, so somebody with the app open on a laptop
            // and a phone hears it on both - see SubjectClaimUserIdProvider for what makes that work.
            await hub.Clients
                .Users([.. userIds.Select(userId => userId.ToString())])
                .SendCoreAsync(message, arguments, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not announce {Message} to {Count} account(s)", message, userIds.Count);
        }
    }
}
