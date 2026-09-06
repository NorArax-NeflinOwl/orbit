using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Orbit.Api.LiveUpdates;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.LiveUpdates;
using Xunit;

namespace Orbit.Api.Tests.LiveUpdates;

/// <summary>
/// The half that only a real PostgreSQL can answer: does an announcement made on one instance actually
/// come out on another?
///
/// Everything around it is unit-testable and is tested in LiveUpdateBackplaneTests. This is not - LISTEN
/// and NOTIFY are the database's, and a fake that accepted both would prove nothing about the channel
/// name, the payload limit, or whether a listener hears a notification sent on a different connection.
///
/// **It does nothing unless ORBIT_TEST_POSTGRES names a database**, so `dotnet test` stays a suite that
/// needs no services, which is what lets it be the check a change gets before it reaches Coding. Run it
/// against the Compose database with:
///
/// <code>
/// docker compose -p orbit up -d postgres
/// ORBIT_TEST_POSTGRES="Host=localhost;Port=5432;Database=orbit;Username=orbit;Password=&lt;POSTGRES_PASSWORD&gt;" \
///   dotnet test tests/Orbit.Api.Tests --filter FullyQualifiedName~PostgresLiveUpdateBackplane
/// </code>
/// </summary>
public sealed class PostgresLiveUpdateBackplaneTests
{
    private static string? ConnectionString => Environment.GetEnvironmentVariable("ORBIT_TEST_POSTGRES");

    /// <summary>
    /// The failure the backplane exists to prevent, staged: two instances, a client connected to the
    /// second, and the announcement made on the first. Before this existed the second instance heard
    /// nothing at all.
    /// </summary>
    [Fact]
    public async Task An_announcement_made_on_one_instance_arrives_on_another()
    {
        if (ConnectionString is null)
        {
            return;
        }

        await using var dataSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var listeningInstance = new LiveUpdateInstance();
        var heard = new RecordingLiveUpdateFanOut();
        var relay = new PostgresLiveUpdateRelay(
            heard, listeningInstance, dataSource, NullLogger<PostgresLiveUpdateRelay>.Instance);

        await relay.StartAsync(stopping.Token);
        await WaitUntilListeningAsync(dataSource, stopping.Token);

        var announcingInstance = new LiveUpdateInstance();
        var announcedLocally = new RecordingLiveUpdateFanOut();
        var announcer = new LiveUpdateAnnouncer(new PostgresLiveUpdateFanOut(
            announcedLocally, announcingInstance, dataSource,
            NullLogger<PostgresLiveUpdateFanOut>.Instance));

        var subject = Guid.NewGuid();
        var audience = Guid.NewGuid();
        await announcer.PresenceChangedAsync(subject, [audience], stopping.Token);

        var arrived = await WaitForAnnouncementAsync(heard, stopping.Token);

        Assert.Equal(LiveUpdateMessages.PresenceChanged, arrived.Message);
        Assert.Equal([audience], arrived.Audience);

        // Arguments cross as JSON and are handed to SignalR in that form - see LiveUpdateAnnouncement.
        var carried = Assert.IsType<JsonElement>(Assert.Single(arrived.Arguments));
        Assert.Equal(subject, carried.GetGuid());

        // The instance that made it still delivered to its own connections directly, without waiting for
        // the database - the local path is not routed through the backplane.
        Assert.Single(announcedLocally.Announcements);

        await relay.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// NOTIFY reaches the sender's own listener too. An instance that acted on that would announce twice
    /// to every client it is holding - once directly, once off the wire - which is a duplicate nobody
    /// would trace back to here.
    /// </summary>
    [Fact]
    public async Task An_instance_does_not_hear_its_own_announcement_twice()
    {
        if (ConnectionString is null)
        {
            return;
        }

        await using var dataSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var instance = new LiveUpdateInstance();
        var delivered = new RecordingLiveUpdateFanOut();
        var relay = new PostgresLiveUpdateRelay(
            delivered, instance, dataSource, NullLogger<PostgresLiveUpdateRelay>.Instance);

        await relay.StartAsync(stopping.Token);
        await WaitUntilListeningAsync(dataSource, stopping.Token);

        var announcer = new LiveUpdateAnnouncer(new PostgresLiveUpdateFanOut(
            delivered, instance, dataSource, NullLogger<PostgresLiveUpdateFanOut>.Instance));

        await announcer.ChatChangedAsync(Guid.NewGuid(), stopping.Token);

        // Long enough that the notification has been round-tripped through the database and discarded.
        await Task.Delay(TimeSpan.FromSeconds(2), stopping.Token);

        Assert.Single(delivered.Announcements);

        await relay.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// The relay registers its LISTEN asynchronously after starting, and a notification sent before that
    /// lands is genuinely lost - LISTEN/NOTIFY keeps nothing for a listener that was not there yet.
    /// Asking PostgreSQL who is listening is the only honest way to know it is safe to announce.
    /// </summary>
    private static async Task WaitUntilListeningAsync(
        NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            // pg_listening_channels() only reports the calling session, so the listener - which is a
            // different connection - has to be found in the activity catalogue instead.
            await using var command = new NpgsqlCommand(
                "SELECT count(*) FROM pg_stat_activity WHERE query LIKE 'LISTEN %' AND state = 'idle'",
                connection);

            var listeners = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
            if (listeners > 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private static async Task<(string Message, IReadOnlyCollection<Guid> Audience, IReadOnlyList<object?> Arguments)>
        WaitForAnnouncementAsync(RecordingLiveUpdateFanOut heard, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (heard.Announcements.Count > 0)
            {
                return heard.Announcements[0];
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        throw new TimeoutException("The announcement never arrived from the other instance.");
    }
}
