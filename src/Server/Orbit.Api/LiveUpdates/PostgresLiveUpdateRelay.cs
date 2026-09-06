using System.Text.Json;
using System.Threading.Channels;
using Npgsql;

namespace Orbit.Api.LiveUpdates;

/// <summary>
/// The listening half of the backplane: holds a PostgreSQL connection open on
/// <see cref="LiveUpdateAnnouncement.ChannelName"/> and hands whatever arrives to this instance's own
/// connections. <see cref="PostgresLiveUpdateFanOut"/> is the half that sends.
///
/// Nothing here is durable and nothing here retries a delivery, which is the correct shape rather than a
/// shortcut. LISTEN/NOTIFY drops anything sent while this is reconnecting, and that is exactly what
/// ILiveUpdatePublisher already promises: a client that was not listening simply did not hear, and
/// answers every announcement by re-reading from the cursor it holds. A backplane that queued these up
/// would be paying to deliver a nudge that has since been overtaken by the poll underneath it.
/// </summary>
public sealed class PostgresLiveUpdateRelay(
    ILocalLiveUpdateFanOut local,
    LiveUpdateInstance instance,
    NpgsqlDataSource dataSource,
    ILogger<PostgresLiveUpdateRelay> logger) : BackgroundService
{
    private static readonly TimeSpan DelayBeforeReconnecting = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Announcements wait here between the connection's callback, which must not block, and the loop
    /// that delivers them. Bounded and dropping the oldest on purpose: these are droppable by design, so
    /// a burst that outruns delivery should cost the stalest nudges rather than the instance's memory.
    /// </summary>
    private readonly Channel<LiveUpdateAnnouncement> _arrived =
        Channel.CreateBounded<LiveUpdateAnnouncement>(
            new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.WhenAll(
            ListenAsync(stoppingToken),
            DeliverWhatArrivesAsync(stoppingToken));
    }

    private async Task ListenAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Deliberately pins one connection from the pool for the lifetime of the process: a
                // listener has to stay on the same connection to keep hearing, since LISTEN is
                // registered per connection rather than per database.
                await using var connection = dataSource.CreateConnection();
                await connection.OpenAsync(stoppingToken);
                connection.Notification += Received;

                // Interpolated because a channel is an identifier and cannot be a parameter. Safe only
                // because it is the constant next door and never anything a request supplies.
                await using (var listen =
                    new NpgsqlCommand($"LISTEN {LiveUpdateAnnouncement.ChannelName}", connection))
                {
                    await listen.ExecuteNonQueryAsync(stoppingToken);
                }

                logger.LogInformation(
                    "Listening for live updates from the other instances on {Channel}",
                    LiveUpdateAnnouncement.ChannelName);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await connection.WaitAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // The instance keeps working without this: it still delivers to its own connections and
                // still tells the others. What it loses is hearing them, so this is worth a warning and
                // worth retrying, and worth neither stopping the app nor a tight loop.
                logger.LogWarning(
                    exception, "Lost the live update connection to PostgreSQL. Reconnecting in {Delay}",
                    DelayBeforeReconnecting);

                await SafeDelayAsync(stoppingToken);
            }
        }
    }

    private void Received(object? sender, NpgsqlNotificationEventArgs notification)
    {
        try
        {
            var announcement = JsonSerializer.Deserialize<LiveUpdateAnnouncement>(notification.Payload);
            if (announcement is null || announcement.Origin == instance.Id)
            {
                // Our own, already delivered locally before it was ever sent. See LiveUpdateInstance.
                return;
            }

            _arrived.Writer.TryWrite(announcement);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Ignored an unreadable live update announcement");
        }
    }

    private async Task DeliverWhatArrivesAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var announcement in _arrived.Reader.ReadAllAsync(stoppingToken))
            {
                await local.AnnounceAsync(
                    announcement.Message,
                    announcement.UserIds,
                    [.. announcement.Arguments.Cast<object?>()],
                    stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private static async Task SafeDelayAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(DelayBeforeReconnecting, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
