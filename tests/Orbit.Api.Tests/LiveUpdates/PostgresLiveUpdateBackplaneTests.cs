using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Orbit.Api.Instances;
using Orbit.Api.LiveUpdates;
using Orbit.Api.Telemetry;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.LiveUpdates;
using Xunit;

namespace Orbit.Api.Tests.LiveUpdates;

/// <summary>
/// The half that only a real PostgreSQL can answer: does something one instance says actually come out
/// on another?
///
/// Everything around it is unit-testable and is tested in LiveUpdateBackplaneTests. This is not - LISTEN
/// and NOTIFY are the database's, and a fake that accepted both would prove nothing about the channel
/// names, the payload limit, or whether a listener hears a notification sent on a different connection.
///
/// **It does nothing unless ORBIT_TEST_POSTGRES names a database**, so `dotnet test` stays a suite that
/// needs no services, which is what lets it be the check a change gets before it reaches Coding. That
/// also means a green suite is not evidence about any of this; the command below, run by hand, is.
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

        var heard = new RecordingLiveUpdateFanOut();
        using var listening = await StartListenerAsync(
            dataSource, stopping.Token, new LiveUpdateNoticeHandler(heard));

        var announcedLocally = new RecordingLiveUpdateFanOut();
        var announcer = new LiveUpdateAnnouncer(
            new PostgresLiveUpdateFanOut(announcedLocally, SenderFor(new InstanceIdentity(), dataSource)));

        var subject = Guid.NewGuid();
        var audience = Guid.NewGuid();
        await announcer.PresenceChangedAsync(subject, [audience], stopping.Token);

        await WaitUntilAsync(() => heard.Announcements.Count > 0, stopping.Token);
        var arrived = heard.Announcements[0];

        Assert.Equal(LiveUpdateMessages.PresenceChanged, arrived.Message);
        Assert.Equal([audience], arrived.Audience);

        // Arguments cross as JSON and are handed to SignalR in that form - see LiveUpdateAnnouncement.
        var carried = Assert.IsType<JsonElement>(Assert.Single(arrived.Arguments));
        Assert.Equal(subject, carried.GetGuid());

        // The instance that made it still delivered to its own connections directly, without waiting for
        // the database - the local path is not routed through the bus.
        Assert.Single(announcedLocally.Announcements);
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

        var instance = new InstanceIdentity();
        var delivered = new RecordingLiveUpdateFanOut();
        using var listening = await StartListenerAsync(
            dataSource, stopping.Token, instance, new LiveUpdateNoticeHandler(delivered));

        var announcer = new LiveUpdateAnnouncer(
            new PostgresLiveUpdateFanOut(delivered, SenderFor(instance, dataSource)));

        await announcer.ChatChangedAsync(Guid.NewGuid(), stopping.Token);

        // Long enough that the notice has been round-tripped through the database and discarded.
        await Task.Delay(TimeSpan.FromSeconds(2), stopping.Token);

        Assert.Single(delivered.Announcements);
    }

    /// <summary>
    /// The privacy fix, staged the way it actually fails: the account changes its choice on one
    /// instance, and a request served by another must not go on being traced against what that one
    /// remembered. Before the notice existed the second instance kept the stale answer for a minute.
    /// </summary>
    [Fact]
    public async Task Changing_the_privacy_choice_clears_what_the_other_instances_remember()
    {
        if (ConnectionString is null)
        {
            return;
        }

        await using var dataSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var account = Guid.NewGuid();

        // The instance that did not serve the change, holding the answer it read a moment ago.
        using var elsewhereMemory = new MemoryCache(new MemoryCacheOptions());
        var elsewhere = new PrivacyChoiceCache(
            elsewhereMemory, SenderFor(new InstanceIdentity(), dataSource));
        elsewhere.Remember(account, keepsThirdPartiesOut: false);
        Assert.True(elsewhere.TryRecall(account, out _));

        using var listening = await StartListenerAsync(
            dataSource, stopping.Token, new PrivacyChoiceNoticeHandler(elsewhere));

        // The instance that served PUT /api/users/me/privacy.
        using var servingMemory = new MemoryCache(new MemoryCacheOptions());
        var serving = new PrivacyChoiceCache(
            servingMemory, SenderFor(new InstanceIdentity(), dataSource));
        await serving.ForgetEverywhereAsync(account, stopping.Token);

        await WaitUntilAsync(() => !elsewhere.TryRecall(account, out _), stopping.Token);
    }

    private static PostgresInstanceNoticeSender SenderFor(
        InstanceIdentity instance, NpgsqlDataSource dataSource)
        => new(instance, dataSource, NullLogger<PostgresInstanceNoticeSender>.Instance);

    private static Task<PostgresInstanceNoticeListener> StartListenerAsync(
        NpgsqlDataSource dataSource, CancellationToken cancellationToken,
        params IInstanceNoticeHandler[] handlers)
        => StartListenerAsync(dataSource, cancellationToken, new InstanceIdentity(), handlers);

    private static async Task<PostgresInstanceNoticeListener> StartListenerAsync(
        NpgsqlDataSource dataSource, CancellationToken cancellationToken,
        InstanceIdentity instance, params IInstanceNoticeHandler[] handlers)
    {
        var listener = new PostgresInstanceNoticeListener(
            handlers, instance, dataSource, NullLogger<PostgresInstanceNoticeListener>.Instance);

        await listener.StartAsync(cancellationToken);
        await WaitUntilListeningAsync(dataSource, handlers.Length, cancellationToken);
        return listener;
    }

    /// <summary>
    /// The listener registers its LISTEN asynchronously after starting, and a notice sent before that
    /// lands is genuinely lost - LISTEN/NOTIFY keeps nothing for a listener that was not there yet.
    /// Asking PostgreSQL who is listening is the only honest way to know it is safe to send.
    /// </summary>
    private static async Task WaitUntilListeningAsync(
        NpgsqlDataSource dataSource, int expected, CancellationToken cancellationToken)
    {
        await WaitUntilAsync(
            async () =>
            {
                await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

                // pg_listening_channels() reports only the calling session, so the listener - which is a
                // different connection - has to be found in the activity catalogue instead.
                await using var command = new NpgsqlCommand(
                    "SELECT count(*) FROM pg_stat_activity WHERE query LIKE 'LISTEN %' AND state = 'idle'",
                    connection);

                var listening = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
                return listening >= expected;
            },
            cancellationToken);
    }

    private static Task WaitUntilAsync(Func<bool> ready, CancellationToken cancellationToken)
        => WaitUntilAsync(() => Task.FromResult(ready()), cancellationToken);

    /// <summary>
    /// A condition rather than a value on purpose. An earlier version of this waited for a value and
    /// treated "not null yet" as "keep waiting", which silently never waits at all when the value is a
    /// struct - the empty tuple is not null, so the first check passed and the assertions ran against
    /// nothing.
    /// </summary>
    private static async Task WaitUntilAsync(
        Func<Task<bool>> ready, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (await ready())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        throw new TimeoutException("What the other instance was told never arrived.");
    }
}
