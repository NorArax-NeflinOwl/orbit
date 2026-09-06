using System.Text.Json;
using Npgsql;

namespace Orbit.Api.LiveUpdates;

/// <summary>
/// Reaches the connections held by *every* API instance, by delivering locally exactly as before and
/// telling the other replicas over PostgreSQL's LISTEN/NOTIFY. <see cref="PostgresLiveUpdateRelay"/> is
/// the half that listens.
///
/// Postgres rather than Redis or Azure SignalR because the database is already here. A backplane is a
/// bus that has to be reachable by every instance and lose nothing that matters, and for announcements
/// - which are allowed to go missing by design, see ILiveUpdatePublisher - the connection Orbit already
/// opens meets that bar without a second resource to pay for, secure and keep alive.
///
/// **Local first, and then the wire.** The local delivery is not routed through Postgres, so the common
/// case - the recipient is connected to the instance that did the work - keeps exactly the latency and
/// the reliability it had before this class existed. NOTIFY is added beside it, never in front of it: if
/// the database refuses the notification, everyone on this instance has still been told, which is
/// precisely the behaviour of the single-replica deployment this replaces. It can only add reach.
///
/// It is not a general SignalR backplane and does not pretend to be one. There are no groups, no
/// client-to-server invocations and no return values to route - Orbit announces four things by account
/// and nothing else - so what would be a HubLifetimeManager elsewhere is a fan-out of names here.
/// </summary>
public sealed class PostgresLiveUpdateFanOut(
    ILocalLiveUpdateFanOut local,
    LiveUpdateInstance instance,
    NpgsqlDataSource dataSource,
    ILogger<PostgresLiveUpdateFanOut> logger) : ILiveUpdateFanOut
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
        await TellTheOtherInstancesAsync(message, userIds, arguments, cancellationToken);
    }

    private async Task TellTheOtherInstancesAsync(
        string message,
        IReadOnlyCollection<Guid> userIds,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

            foreach (var announcement in
                LiveUpdateAnnouncement.ForAudience(instance.Id, message, userIds, arguments))
            {
                // pg_notify rather than a NOTIFY statement, so the payload travels as a parameter. The
                // statement form would need the payload quoted into SQL text, and the accounts named in
                // it come from user data.
                await using var command = new NpgsqlCommand("SELECT pg_notify(@channel, @payload)", connection);
                command.Parameters.AddWithValue("channel", LiveUpdateAnnouncement.ChannelName);
                command.Parameters.AddWithValue("payload", JsonSerializer.Serialize(announcement));
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        catch (Exception exception)
        {
            // Swallowed for the reason the local delivery swallows its own: an announcement is the
            // shortcut, not the work. Everyone on this instance has already been told by the time this
            // runs, so a failure here costs the other replicas a nudge and their clients one slow poll.
            logger.LogWarning(
                exception, "Could not tell the other instances about {Message} for {Count} account(s)",
                message, userIds.Count);
        }
    }
}
