using System.Text.Json;
using Orbit.Api.Instances;

namespace Orbit.Api.LiveUpdates;

/// <summary>
/// Reaches the connections held by *every* API instance, by delivering locally exactly as before and
/// telling the others over <see cref="PostgresInstanceNoticeSender"/>.
/// <see cref="LiveUpdateNoticeHandler"/> is the half that receives.
///
/// **Local first, and then the wire.** The local delivery is not routed through the notice bus, so the
/// common case - the recipient is connected to the instance that did the work - keeps exactly the
/// latency and the reliability it had before this class existed. The notice is added beside it, never in
/// front of it: if the database refuses it, everyone on this instance has still been told, which is
/// precisely the behaviour of the single-replica deployment this replaces. It can only add reach.
///
/// It is not a general SignalR backplane and does not pretend to be one. There are no groups, no
/// client-to-server invocations and no return values to route - Orbit announces four things by account
/// and nothing else - so what would be a HubLifetimeManager elsewhere is a fan-out of names here.
/// </summary>
public sealed class PostgresLiveUpdateFanOut(
    ILocalLiveUpdateFanOut local,
    PostgresInstanceNoticeSender notices) : ILiveUpdateFanOut
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

        await local.AnnounceAsync(message, userIds, arguments, cancellationToken);

        foreach (var announcement in
            LiveUpdateAnnouncement.ForAudience(message, userIds, arguments))
        {
            await notices.SendAsync(
                LiveUpdateAnnouncement.Channel,
                JsonSerializer.Serialize(announcement),
                cancellationToken);
        }
    }
}
