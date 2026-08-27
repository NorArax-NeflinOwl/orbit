using Orbit.Mobile.Api;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Presence;
using Orbit.Mobile.Screens.Navigation;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The bar across the top of every signed-in screen. Small, but it is on every page, so the two things
/// it can get wrong - who it says is signed in, and whether it claims there is something unread - are
/// wrong everywhere at once.
/// </summary>
public sealed class NavigationBarTests
{
    [Fact]
    public async Task The_avatar_shows_the_reader_initials()
    {
        var context = new BarContext("Ala Kowalska");

        var bar = context.Open();
        await bar.LoadCommand.ExecuteAsync(null);

        Assert.Equal("AK", bar.Initials);
    }

    [Fact]
    public async Task A_one_word_name_still_gets_an_avatar()
    {
        var context = new BarContext("Ala");

        var bar = context.Open();
        await bar.LoadCommand.ExecuteAsync(null);

        Assert.Equal("A", bar.Initials);
    }

    [Fact]
    public async Task Unread_notifications_are_badged()
    {
        var context = new BarContext("Ala");
        context.Server.Add("New message", "/map");
        context.Server.Add("Overdue task", "/tasks/00000000-0000-0000-0000-000000000001");

        var bar = context.Open();
        await bar.LoadCommand.ExecuteAsync(null);

        Assert.True(bar.HasUnread);
        Assert.Equal("2", bar.UnreadLabel);
    }

    [Fact]
    public async Task Nothing_unread_means_no_badge_at_all()
    {
        var context = new BarContext("Ala");
        context.Server.Add("Seen already", "/map", isRead: true);

        var bar = context.Open();
        await bar.LoadCommand.ExecuteAsync(null);

        Assert.False(bar.HasUnread);
    }

    [Fact]
    public async Task Being_offline_leaves_the_bar_usable()
    {
        // The initials come from the stored session, so the bar is complete without a connection. A
        // missing badge reads as "nothing new", which is the safer of the two wrong answers.
        var context = new BarContext("Ala Kowalska");
        context.Server.IsUnreachable = true;

        var bar = context.Open();
        await bar.LoadCommand.ExecuteAsync(null);

        Assert.Equal("AK", bar.Initials);
        Assert.False(bar.HasUnread);
    }

    [Fact]
    public void The_logo_leads_to_the_dashboard()
    {
        var context = new BarContext("Ala");
        var bar = context.Open();

        bar.GoToDashboardCommand.Execute(null);

        Assert.Equal("ShowDashboard", context.Navigator.LastDestination);
    }

    private sealed class BarContext
    {
        private readonly SessionStore _sessionStore;

        public BarContext(string displayName)
            => _sessionStore = new SessionStore(new InMemorySessionStorage(
                new UserSession("access", "refresh", Guid.NewGuid(), "me@orbit.example", displayName)));

        public FakeNotificationServer Server { get; } = new();

        public RecordingScreenNavigator Navigator { get; } = new();

        public NavigationBarViewModel Open()
            => new(
                _sessionStore, new NotificationsClient(Server.ToHttpClient()),
                new Orbit.Mobile.Presence.Presence(
                    FixedNetworkStatus.Online, new InMemoryPresenceStore(),
                    new FakeTimeProvider(DateTimeOffset.Parse("2026-08-27T09:00:00Z"))),
                Navigator);
    }
}
