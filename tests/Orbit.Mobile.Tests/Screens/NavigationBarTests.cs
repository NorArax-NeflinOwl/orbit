using Orbit.Mobile.Api;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
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
    public void The_avatar_opens_a_menu_rather_than_going_anywhere()
    {
        // Orbit.Web's avatar does the same: the account, the notifications and signing out all hang off
        // it, and making the avatar mean one of them would hide the other two.
        var context = new BarContext("Ala");
        var bar = context.Open();

        bar.ToggleMenuCommand.Execute(null);

        Assert.True(bar.IsMenuOpen);
        Assert.Empty(context.Navigator.Destinations);
    }

    [Fact]
    public void Leaving_the_menu_for_somewhere_closes_it()
    {
        var context = new BarContext("Ala");
        var bar = context.Open();
        bar.ToggleMenuCommand.Execute(null);

        bar.GoToNotificationsCommand.Execute(null);

        // Otherwise coming back to a page finds the menu hanging open over it.
        Assert.False(bar.IsMenuOpen);
        Assert.Equal("ShowNotifications", context.Navigator.LastDestination);
    }

    [Fact]
    public void Setting_a_status_leaves_the_menu_open()
    {
        // Setting a status is not leaving the menu, and closing it would hide the dot the reader just
        // changed before they could see it change.
        var context = new BarContext("Ala");
        var bar = context.Open();
        bar.ToggleMenuCommand.Execute(null);

        bar.ChooseUnavailableCommand.Execute(null);

        Assert.True(bar.IsMenuOpen);
        Assert.False(bar.IsAvailable);
        Assert.Equal(PresenceAppearance.Unavailable, bar.Appearance);
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

        public LocalStore LocalStore { get; } = new();

        public FakeNotificationServer Server { get; } = new();

        public RecordingScreenNavigator Navigator { get; } = new();

        public NavigationBarViewModel Open()
            => new(
                _sessionStore, new NotificationsClient(Server.ToHttpClient()),
                new AuthenticationClient(Server.ToHttpClient(), FixedNetworkStatus.Online, _sessionStore),
                Presence, new Translations(new InMemoryLanguageStore()),
                new LocalStoreReset(LocalStore), Navigator);

        public Orbit.Mobile.Presence.Presence Presence { get; } = new(
            FixedNetworkStatus.Online, new InMemoryPresenceStore(),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-08-27T09:00:00Z")));
    }
}
