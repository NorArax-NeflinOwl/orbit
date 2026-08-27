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

    private sealed class ReportingContext : IDisposable
    {
        private readonly SessionStore _sessionStore;

        public ReportingContext(bool signedIn = true)
        {
            _sessionStore = new SessionStore(new InMemorySessionStorage(signedIn
                ? new UserSession("access", "refresh", Guid.NewGuid(), "me@orbit.example", "Me")
                : null));

            Presence = new Orbit.Mobile.Presence.Presence(
                FixedNetworkStatus.Online, new InMemoryPresenceStore(),
                new FakeTimeProvider(DateTimeOffset.Parse("2026-08-27T10:00:00Z")));
        }

        public FakePresenceServer Server { get; } = new();

        public Orbit.Mobile.Presence.Presence Presence { get; }

        public PresenceReporter Reporter()
            => new(Presence, new UsersClient(Server.ToHttpClient()), _sessionStore,
                NullLogger<PresenceReporter>.Instance);

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
