namespace Orbit.Core.LiveUpdates;

/// <summary>
/// Tells whoever is connected right now that something they are looking at has changed, so a client can
/// stop asking. Implemented outside Orbit.Core - see SignalRLiveUpdatePublisher - the same separation
/// <see cref="Notifications.IPushNotificationSender"/> gives push delivery: the domain says what
/// happened, and nothing here knows a WebSocket exists.
///
/// Every method announces a change without carrying it. That is deliberate and it is the whole design:
/// the client already knows how to fetch and decrypt its own data, and chat messages are end-to-end
/// encrypted, so a publisher that carried content would need a plaintext the server does not have. A
/// nudge costs one fetch and keeps the server exactly as ignorant as it is today.
///
/// It also makes a missed announcement harmless rather than a lost message. Nothing here is delivered
/// reliably - a client that was reconnecting when this fired simply did not hear it - and because the
/// answer to every announcement is "read again from the cursor you already hold", hearing it late or not
/// at all costs a delay, never a message. This is why the clients keep a slow poll running underneath.
/// </summary>
public interface ILiveUpdatePublisher
{
    /// <summary>
    /// Something in <paramref name="userId"/>'s chat changed: a message arrived or was edited, or a
    /// conversation was approved. Announced to that one person, who then re-reads the conversation they
    /// happen to have open - which is why this does not say which conversation it was.
    /// </summary>
    Task ChatChangedAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The same, for each of several people at once - a group message reaches every member.</summary>
    Task ChatChangedAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    /// <summary>Something arrived in, or left, <paramref name="userId"/>'s notification feed.</summary>
    Task NotificationsChangedAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// <paramref name="userId"/> came, went, or changed what they are showing as. Announced to the
    /// people who can see it rather than broadcast: presence is only visible between contacts, and a
    /// client that cannot see somebody's status has no business being told it changed.
    /// </summary>
    Task PresenceChangedAsync(Guid userId, IReadOnlyCollection<Guid> visibleToUserIds, CancellationToken cancellationToken);
}
