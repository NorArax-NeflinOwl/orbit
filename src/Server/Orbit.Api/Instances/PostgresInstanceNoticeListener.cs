using System.Text.Json;
using System.Threading.Channels;
using Npgsql;

namespace Orbit.Api.Instances;

/// <summary>
/// Holds one PostgreSQL connection open, LISTENing on every channel a handler asked for, and hands what
/// arrives to that handler. <see cref="PostgresInstanceNoticeSender"/> is the half that sends.
///
/// One connection for all of them rather than one each: LISTEN is registered per connection and a
/// listener has to stay on the same one to keep hearing, so each channel would otherwise pin a
/// connection of its own for the lifetime of the process.
///
/// Nothing here is durable and nothing retries. A notice sent while this was reconnecting is genuinely
/// lost, and that is the contract rather than a shortcut - see <see cref="IInstanceNoticeHandler"/>.
/// </summary>
public sealed class PostgresInstanceNoticeListener(
    IEnumerable<IInstanceNoticeHandler> handlers,
    InstanceIdentity instance,
    NpgsqlDataSource dataSource,
    ILogger<PostgresInstanceNoticeListener> logger) : BackgroundService
{
    private static readonly TimeSpan DelayBeforeReconnecting = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Notices wait here between the connection's callback, which must not block, and the loop that
    /// runs the handlers. Bounded and dropping the oldest on purpose: these are droppable by design, so
    /// a burst that outruns handling should cost the stalest notices rather than the instance's memory.
    /// </summary>
    private readonly Channel<(IInstanceNoticeHandler Handler, string Body)> _arrived =
        Channel.CreateBounded<(IInstanceNoticeHandler, string)>(
            new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });

    private readonly Dictionary<string, IInstanceNoticeHandler> _byChannel =
        handlers.ToDictionary(handler => handler.Channel);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_byChannel.Count == 0)
        {
            return;
        }

        await Task.WhenAll(
            ListenAsync(stoppingToken),
            HandleWhatArrivesAsync(stoppingToken));
    }

    private async Task ListenAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = dataSource.CreateConnection();
                await connection.OpenAsync(stoppingToken);
                connection.Notification += Received;

                foreach (var channel in _byChannel.Keys)
                {
                    // Interpolated because a channel is an identifier and cannot be a parameter. Safe
                    // only because every one of these comes from a handler's own constant, never from
                    // anything a request supplies.
                    await using var listen = new NpgsqlCommand($"LISTEN {channel}", connection);
                    await listen.ExecuteNonQueryAsync(stoppingToken);
                }

                logger.LogInformation(
                    "Listening for notices from the other instances on {Channels}",
                    string.Join(", ", _byChannel.Keys));

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
                // The instance keeps working without this: it still does its own work and still tells
                // the others. What it loses is hearing them - worth a warning, worth retrying, and worth
                // neither stopping the app nor a tight loop.
                logger.LogWarning(
                    exception, "Lost the notice connection to PostgreSQL. Reconnecting in {Delay}",
                    DelayBeforeReconnecting);

                await DelayAsync(stoppingToken);
            }
        }
    }

    private void Received(object? sender, NpgsqlNotificationEventArgs notification)
    {
        try
        {
            var notice = JsonSerializer.Deserialize<InstanceNotice>(notification.Payload);
            if (notice is null || notice.Origin == instance.Id)
            {
                // Our own. Whatever it says, this instance did it before it sent - see InstanceIdentity.
                return;
            }

            if (_byChannel.TryGetValue(notification.Channel, out var handler))
            {
                _arrived.Writer.TryWrite((handler, notice.Body));
            }
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception, "Ignored an unreadable notice on {Channel}", notification.Channel);
        }
    }

    private async Task HandleWhatArrivesAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var (handler, body) in _arrived.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await handler.HandleAsync(body, stoppingToken);
                }
                catch (Exception exception)
                {
                    // One handler failing must not take the listener down with it and leave every other
                    // channel deaf for the rest of the process's life.
                    logger.LogWarning(
                        exception, "A notice on {Channel} could not be handled", handler.Channel);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private static async Task DelayAsync(CancellationToken stoppingToken)
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
