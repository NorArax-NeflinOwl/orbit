using System.Text;
using System.Text.Json;
using Npgsql;

namespace Orbit.Api.Instances;

/// <summary>
/// Tells the other API instances something, over PostgreSQL's NOTIFY.
///
/// PostgreSQL rather than Redis or a message broker because the database is already here, already
/// reachable from every instance and already paid for. What rides on it is only ever best-effort - see
/// <see cref="IInstanceNoticeHandler"/> - which is the bar LISTEN/NOTIFY meets and the reason a durable
/// bus would be paying for a guarantee nothing here wants.
///
/// It never throws. Every caller has already done its own work by the time it gets here, and a notice
/// that cannot be sent must not turn a request that succeeded into one that failed.
/// </summary>
public sealed class PostgresInstanceNoticeSender(
    InstanceIdentity instance,
    NpgsqlDataSource dataSource,
    ILogger<PostgresInstanceNoticeSender> logger)
{
    public async Task SendAsync(string channel, string body, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new InstanceNotice(instance.Id, body));
        if (Encoding.UTF8.GetByteCount(payload) > InstanceNotice.MaxPayloadBytes)
        {
            // A sender that can outgrow the limit is meant to split before it gets here. Refusing loudly
            // in the log beats a PostgresException surfacing in whatever request happened to trigger it.
            logger.LogError(
                "A notice on {Channel} was too large to send and was dropped. This is a bug in whatever "
                + "built it: bodies over {Limit} bytes must be split by the sender.",
                channel, InstanceNotice.MaxPayloadBytes - InstanceNotice.EnvelopeBytes);
            return;
        }

        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

            // pg_notify rather than a NOTIFY statement, so the payload travels as a parameter. The
            // statement form would need it quoted into SQL text, and bodies carry user data.
            await using var command = new NpgsqlCommand("SELECT pg_notify(@channel, @payload)", connection);
            command.Parameters.AddWithValue("channel", channel);
            command.Parameters.AddWithValue("payload", payload);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not tell the other instances about {Channel}", channel);
        }
    }
}
