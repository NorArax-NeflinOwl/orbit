using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Core.Users;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Presence;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Presence;

/// <summary>
/// The half of presence that leaves the phone. Until the server had any notion of it, the dot on the
/// avatar told the reader only what they had set; now the same choice has to reach the people who see
/// them in their own contact list.
/// </summary>
public sealed class PresenceReportingTests
{
    [Fact]
    public async Task Choosing_to_be_unavailable_tells_the_server()
    {
        using var context = new ReportingContext();
        using var reporter = context.Reporter();

        context.Presence.Choose(ChosenAvailability.Unavailable);
        await context.SettleAsync();

        Assert.Equal(nameof(PresenceAvailability.DoNotDisturb), context.Server.Availability);
    }

    [Fact]
    public async Task Choosing_to_be_available_again_tells_the_server()
    {
        using var context = new ReportingContext();
        using var reporter = context.Reporter();

        context.Presence.Choose(ChosenAvailability.Unavailable);
        await context.SettleAsync();
        context.Presence.Choose(ChosenAvailability.Available);
        await context.SettleAsync();

        Assert.Equal(nameof(PresenceAvailability.Available), context.Server.Availability);
    }

    /// <summary>
    /// Nothing is reported for nobody. A choice made at the sign-in screen - the language row sits
    /// beside the status one - would otherwise go out as an unauthenticated request.
    /// </summary>
    [Fact]
    public async Task Nothing_is_reported_while_nobody_is_signed_in()
    {
        using var context = new ReportingContext(signedIn: false);
        using var reporter = context.Reporter();

        context.Presence.Choose(ChosenAvailability.Unavailable);
        await context.SettleAsync();

        Assert.Null(context.Server.Availability);
    }

    /// <summary>
    /// Being unreachable is not worth reporting to the reader: the choice is already kept on the phone,
    /// and this used to be the sort of thing that took the app down from an unobserved task.
    /// </summary>
    [Fact]
    public async Task A_choice_that_cannot_be_sent_is_not_an_error()
    {
        using var context = new ReportingContext();
        context.Server.IsUnreachable = true;
        using var reporter = context.Reporter();

        context.Presence.Choose(ChosenAvailability.Unavailable);
        await context.SettleAsync();

        Assert.Null(context.Server.Availability);
    }

    /// <summary>
    /// A refused choice used to be lost for good: the phone kept showing "do not disturb", the server
    /// never heard of it, and everybody else went on seeing this account as available. The heartbeat is
    /// already a tick, so it carries the retry.
    /// </summary>
    [Fact]
    public async Task A_choice_the_server_turned_down_goes_out_with_the_next_heartbeat()
    {
        using var context = new ReportingContext();
        context.Server.RefusesAvailability = true;
        using var reporter = context.Reporter();
        reporter.Start();

        context.Presence.Choose(ChosenAvailability.Unavailable);
        await context.SettleAsync();
        Assert.Null(context.Server.Availability);

        context.Server.RefusesAvailability = false;
        await context.BeatAsync();

        Assert.Equal(nameof(PresenceAvailability.DoNotDisturb), context.Server.Availability);
    }

    /// <summary>
    /// With no connection at all the heartbeat stops rather than asking every twenty seconds (see
    /// PresenceReporter.BeatAsync), so the retry waits for whatever starts it again - a resume, a
    /// sign-in - instead of being dropped there.
    /// </summary>
    [Fact]
    public async Task A_choice_lost_to_no_connection_goes_out_when_reporting_starts_again()
    {
        using var context = new ReportingContext();
        context.Server.IsUnreachable = true;
        using var reporter = context.Reporter();
        reporter.Start();

        context.Presence.Choose(ChosenAvailability.Unavailable);
        await context.SettleAsync();
        Assert.Null(context.Server.Availability);

        context.Server.IsUnreachable = false;
        reporter.Stop();
        reporter.Start();
        await context.SettleAsync();

        Assert.Equal(nameof(PresenceAvailability.DoNotDisturb), context.Server.Availability);
    }

    /// <summary>
    /// A choice the server took needs no second telling - a heartbeat that re-sent it every twenty
    /// seconds would be saying something nobody asked for.
    /// </summary>
    [Fact]
    public async Task A_choice_the_server_took_is_not_sent_again()
    {
        using var context = new ReportingContext();
        using var reporter = context.Reporter();
        reporter.Start();

        context.Presence.Choose(ChosenAvailability.Unavailable);
        await context.SettleAsync();
        var afterTheChoice = context.Server.RequestCount;

        await context.BeatAsync();

        // The heartbeat itself, and nothing else.
        Assert.Equal(afterTheChoice + 1, context.Server.RequestCount);
    }

    /// <summary>
    /// A heartbeat over a connection already open costs a frame; as a request of its own it costs a
    /// handshake and a round trip every twenty seconds. That saving is most of why the hub takes
    /// presence at all - see Orbit.Api's LiveUpdatesHub.
    /// </summary>
    [Fact]
    public async Task A_heartbeat_goes_over_the_live_connection_when_there_is_one()
    {
        using var context = new ReportingContext();
        context.LiveUpdates.Becomes(connected: true);
        using var reporter = context.Reporter();
        reporter.Start();

        await context.BeatAsync();

        Assert.Equal(0, context.Server.HeartbeatCount);
        Assert.True(context.LiveUpdates.PresenceReports > 0);
    }

    /// <summary>
    /// And it is the request it always was when there is not. The connection speeds this up; it is not
    /// what makes it work, and a phone on a network that will not carry one must still be seen.
    /// </summary>
    [Fact]
    public async Task A_heartbeat_falls_back_to_the_request_with_no_live_connection()
    {
        using var context = new ReportingContext();
        using var reporter = context.Reporter();
        reporter.Start();

        await context.BeatAsync();

        Assert.True(context.Server.HeartbeatCount > 0);
        Assert.Equal(0, context.LiveUpdates.PresenceReports);
    }

    private sealed class ReportingContext : IDisposable
    {
        private readonly SessionStore _sessionStore;

        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-27T10:00:00Z"));

        public ReportingContext(bool signedIn = true)
        {
            _sessionStore = new SessionStore(new InMemorySessionStorage(signedIn
                ? new UserSession("access", "refresh", Guid.NewGuid(), "me@orbit.example", "Me")
                : null));

            Presence = new Orbit.Mobile.Presence.Presence(
                FixedNetworkStatus.Online, new InMemoryPresenceStore(), _clock);
        }

        public FakePresenceServer Server { get; } = new();

        public Orbit.Mobile.Presence.Presence Presence { get; }

        /// <summary>
        /// The live connection, disconnected by default - so these keep testing the request the
        /// reporter has always made, and one test can turn it on to check the cheaper path.
        /// </summary>
        public AnnouncedLiveUpdates LiveUpdates { get; } = new();

        public PresenceReporter Reporter()
            => new(Presence, new UsersClient(Server.ToHttpClient()), _sessionStore,
                NullLogger<PresenceReporter>.Instance, _clock, LiveUpdates);

        /// <summary>
        /// Lets one heartbeat fall due and waits for what it sends. The timer runs on this clock, so
        /// the twenty seconds cost nothing.
        /// </summary>
        public async Task BeatAsync()
        {
            var alreadySeen = Server.RequestCount;
            _clock.Advance(TimeSpan.FromSeconds(21));

            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (Server.RequestCount == alreadySeen && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }

            // The heartbeat may follow the re-sent choice a moment later; both belong to this beat.
            await Task.Delay(50);
        }

        /// <summary>
        /// Reporting a choice is started without being awaited - the reader's tap must not wait on a
        /// request - so a test has to let it finish. Bounded, because two of these are about a request
        /// that never happens, and waiting for one of those has no natural end.
        /// </summary>
        public async Task SettleAsync()
        {
            var alreadySeen = Server.RequestCount;
            var deadline = DateTime.UtcNow.AddSeconds(2);

            while (Server.RequestCount == alreadySeen && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }
        }

        public void Dispose() => Server.Dispose();
    }
}
