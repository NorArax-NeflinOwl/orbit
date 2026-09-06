using Microsoft.AspNetCore.SignalR;

namespace Orbit.Api.LiveUpdates;

/// <summary>
/// Delivers an announcement down whatever connections *this instance* is holding, over SignalR - named
/// for the transport the way VapidPushNotificationSender and FirebasePushNotificationSender are.
///
/// "This instance" is the whole of the limitation and the reason <see cref="PostgresLiveUpdateFanOut"/>
/// exists: an IHubContext knows only the connections its own process accepted, so with a second replica
/// running, an announcement made here reaches nobody connected there.
///
/// Every method here swallows its own failures. Announcing is not the work; it is the shortcut that
/// saves the client from asking. The message is already written down and the client already knows how to
/// find it, so an announcement that cannot be delivered must never fail the request that caused it - a
/// sent message should not come back as an error because a WebSocket was in the middle of reconnecting.
/// </summary>
public sealed class SignalRLiveUpdateFanOut(
    IHubContext<LiveUpdatesHub> hub,
    ILogger<SignalRLiveUpdateFanOut> logger) : ILocalLiveUpdateFanOut
{
    public async Task AnnounceAsync(
        string message,
        IReadOnlyCollection<Guid> userIds,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken)
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
                .SendCoreAsync(message, [.. arguments], cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not announce {Message} to {Count} account(s)", message, userIds.Count);
        }
    }
}
