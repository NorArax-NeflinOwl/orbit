using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Core.Mobile;
using Orbit.Mobile.Update;
using Orbit.Mobile.Api;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Presence;
using Orbit.Mobile.Screens.Navigation;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;
using Orbit.Mobile.Sync;

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

    /// <summary>
    /// Two letters, as Orbit.Web's AvatarHelper does it - the same person must not read differently in
    /// the browser and on the phone, and single-word display names are common enough that a rule of our
    /// own here was visible on every screen.
    /// </summary>
    [Fact]
    public async Task A_one_word_name_still_gets_an_avatar()
    {
        var context = new BarContext("Ala");

        var bar = context.Open();
        await bar.LoadCommand.ExecuteAsync(null);

        Assert.Equal("AL", bar.Initials);
    }

    [Fact]
    public async Task A_single_letter_name_gets_the_one_letter_there_is()
    {
        var context = new BarContext("A");

        var bar = context.Open();
        await bar.LoadCommand.ExecuteAsync(null);

        Assert.Equal("A", bar.Initials);
    }

    /// <summary>The web takes the first two words rather than the first and the last; this follows it.</summary>
    [Fact]
    public async Task A_name_of_three_words_takes_the_first_two()
    {
        var context = new BarContext("Ala Maria Kowalska");

        var bar = context.Open();
        await bar.LoadCommand.ExecuteAsync(null);

        Assert.Equal("AM", bar.Initials);
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

    /// <summary>
    /// Startup says a newer build is out once, in a prompt the reader is free to dismiss. The badge is
    /// what is left standing afterwards, and the only sign there is anything to do about it.
    /// </summary>
    [Fact]
    public async Task A_newer_version_is_badged_in_the_menu()
    {
        var context = new BarContext("Ala");
        context.RememberANewerVersionIsOut();

        var bar = context.Open();
        await bar.LoadCommand.ExecuteAsync(null);

        Assert.True(bar.IsUpdateAvailable);
    }

    /// <summary>
    /// Read from what startup already learned rather than asked again, so it is right on a train and
    /// costs nothing on every screen - the server here is unreachable and the answer still arrives.
    /// </summary>
    [Fact]
    public async Task Nothing_newer_leaves_the_badge_off()
    {
        var context = new BarContext("Ala");

        var bar = context.Open();
        await bar.LoadCommand.ExecuteAsync(null);

        Assert.False(bar.IsUpdateAvailable);
    }

    [Fact]
    public void The_update_row_leads_to_where_a_newer_build_is()
    {
        var context = new BarContext("Ala");
        var bar = context.Open();
        bar.ToggleMenuCommand.Execute(null);

        bar.GoToUpdateCommand.Execute(null);

        Assert.Equal("ShowUpdate", context.Navigator.LastDestination);
        Assert.False(bar.IsMenuOpen);
    }

    [Fact]
    public void The_logo_leads_to_the_dashboard()
    {
        var context = new BarContext("Ala");
        var bar = context.Open();

        bar.GoToDashboardCommand.Execute(null);

        Assert.Equal("ShowDashboard", context.Navigator.LastDestination);
    }

    /// <summary>
    /// The line moved here from a strip in the bottom-left corner of all twenty screens. It says the
    /// same things, and still says nothing at all before anything has tried - claiming "synced" then
    /// would be a claim, and "offline" a slander.
    /// </summary>
    [Fact]
    public void The_bar_says_whether_the_app_is_in_step()
    {
        var context = new BarContext("Ala");
        var bar = context.Open();

        Assert.Equal(string.Empty, bar.SyncLabel);

        context.SyncState.RecordStarted();
        Assert.True(bar.IsSyncing);
        Assert.Equal("Syncing…", bar.SyncLabel);

        context.SyncState.RecordSucceeded();
        Assert.False(bar.IsSyncing);
        Assert.Equal("Synced", bar.SyncLabel);
    }

    /// <summary>
    /// The way back when a phone loses its connection: try again now, rather than wait for whatever
    /// would have tried next. Offered only while it is needed - a button that is always there invites
    /// tapping at a working app.
    /// </summary>
    [Fact]
    public void Reconnecting_is_offered_only_while_the_phone_is_offline()
    {
        var context = new BarContext("Ala");
        context.Network.Becomes(false);
        var bar = context.Open();

        Assert.True(bar.CanReconnect);

        context.Network.Becomes(true);
        Assert.False(bar.CanReconnect);
    }

    /// <summary>
    /// The row and the button have to agree. "Synced" beside an offer to reconnect is two answers to one
    /// question, and the reassuring one is the wrong one: it was true when it was said and is not now.
    /// </summary>
    [Fact]
    public void An_offline_phone_says_so_rather_than_that_it_is_in_step()
    {
        var context = new BarContext("Ala");
        var bar = context.Open();
        context.Network.Becomes(false);

        Assert.Contains("connection", bar.SyncLabel);
        Assert.False(bar.IsSyncing);
    }

    [Fact]
    public void An_online_phone_is_not_offered_it_at_all()
    {
        var context = new BarContext("Ala");

        Assert.False(context.Open().CanReconnect);
    }

    /// <summary>
    /// It cannot put a phone back on a network - no app can - so what it must not do is throw when the
    /// attempt fails, which is the ordinary case for a button that only appears when things are broken.
    /// </summary>
    [Fact]
    public async Task Reconnecting_where_nobody_answers_is_not_an_error()
    {
        var context = new BarContext("Ala");
        context.Network.Becomes(false);
        var bar = context.Open();

        await bar.ReconnectCommand.ExecuteAsync(null);

        Assert.True(bar.CanReconnect);
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

        /// <summary>Whether the phone is on a network, which is what decides the Reconnect button.</summary>
        public FixedNetworkStatus Network { get; } = FixedNetworkStatus.Online;

        /// <summary>Whether the app is in step, which the bar now says beside the name in its menu.</summary>
        public SyncState SyncState { get; } = new(
            FixedNetworkStatus.Online, new FakeTimeProvider(DateTimeOffset.Parse("2026-08-27T09:00:00Z")));

        /// <summary>
        /// What the app already knows about its own version, which is where the bar's update badge
        /// comes from - see NavigationBarViewModel.IsUpdateAvailable. Nothing remembered by default,
        /// which is a build that has never been told there is anything newer.
        /// </summary>
        public InMemoryVersionVerdictCache Versions { get; } = new();

        /// <summary>Remembers that a newer build exists, as startup would have.</summary>
        public void RememberANewerVersionIsOut()
            => Versions.WriteAsync(
                new CachedVersionVerdict(
                    InstalledVersion, MobileVersionVerdict.UpdateAvailable, "1.4.0", "https://orbit.example/apk"),
                CancellationToken.None).GetAwaiter().GetResult();

        private const string InstalledVersion = "1.3.0";

        public NavigationBarViewModel Open()
            => new(
                _sessionStore, new NotificationsClient(Server.ToHttpClient()),
                new AuthenticationClient(Server.ToHttpClient(), FixedNetworkStatus.Online, _sessionStore),
                Presence, new Translations(new InMemoryLanguageStore()),
                new LocalStoreReset(LocalStore), UnlockedPermissions.For(LocalStore), SyncState,
                new MobileVersionGate(
                    new AppVersion(MobilePlatform.Android, InstalledVersion),
                    // Unreachable on purpose: the bar reads what is remembered and asks nobody.
                    StubHttpMessageHandler.Unreachable().ToHttpClient(), Versions,
                    NullLogger<MobileVersionGate>.Instance),
                // Unreachable too: the About row asks for the server's version and leaves it unsaid when
                // nobody answers, which is the ordinary case on a phone.
                new ServerVersionClient(StubHttpMessageHandler.Unreachable().ToHttpClient()),
                Navigator,
                Synchronizers.AgainstNobody(
                    LocalStore, new ChatRepository(LocalStore, TimeProvider.System),
                    UnlockedPermissions.For(LocalStore), _sessionStore),
                Network);

        public Orbit.Mobile.Presence.Presence Presence { get; } = new(
            FixedNetworkStatus.Online, new InMemoryPresenceStore(),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-08-27T09:00:00Z")));
    }
}
