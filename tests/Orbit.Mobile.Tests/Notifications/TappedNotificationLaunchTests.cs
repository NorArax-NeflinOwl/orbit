using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Notifications;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.Crypto;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Notifications;

/// <summary>
/// Opening the app by tapping a notification.
///
/// The awkward case is cold start: the tap can *launch* Orbit, and iOS hands the payload over long
/// before there is a session, a database, or a screen to replace. That is why the destination is held
/// rather than raised as an event - an event fired before anybody subscribed is simply lost, which is
/// precisely the case that has to work.
/// </summary>
public sealed class TappedNotificationLaunchTests
{
    [Fact]
    public void A_tap_recorded_before_anything_is_listening_is_still_there_afterwards()
    {
        var pending = new PendingNotificationTap();

        pending.Record("/map");

        Assert.Equal("/map", pending.TakeAtStartup());
    }

    [Fact]
    public void A_tap_is_followed_once_rather_than_every_time_somebody_looks()
    {
        var pending = new PendingNotificationTap();
        pending.Record("/map");

        pending.TakeAtStartup();

        // Otherwise every later resume would yank the reader back to the same screen.
        Assert.Null(pending.TakeAtStartup());
    }

    [Fact]
    public void A_tap_arriving_before_startup_waits_rather_than_announcing_itself()
    {
        // Announcing it here too would have the app follow it while the startup flow is still deciding
        // where to open - and startup, running a moment later, would replace whatever it landed on.
        var pending = new PendingNotificationTap();
        var announced = new List<string>();
        pending.RecordedWhileRunning += (_, url) => announced.Add(url);

        pending.Record("/map");

        Assert.Empty(announced);
        Assert.Equal("/map", pending.TakeAtStartup());
    }

    [Fact]
    public void A_tap_arriving_once_the_app_is_running_announces_itself()
    {
        // Found on a device. The obvious wiring - follow the tap when the window resumes - looks right
        // and does nothing, because iOS resumes the app before delivering the tap, so the holder is
        // still empty when Resumed fires. The tap has to be what raises it.
        var pending = new PendingNotificationTap();
        var announced = new List<string>();
        pending.RecordedWhileRunning += (_, url) => announced.Add(url);
        pending.TakeAtStartup();

        pending.Record("/inventory");

        Assert.Equal("/inventory", Assert.Single(announced));
    }

    [Fact]
    public void A_running_app_is_not_told_about_a_notification_carrying_no_destination()
    {
        var pending = new PendingNotificationTap();
        var announced = new List<string>();
        pending.RecordedWhileRunning += (_, url) => announced.Add(url);
        pending.TakeAtStartup();

        pending.Record(url: null);

        Assert.Empty(announced);
    }

    [Fact]
    public void A_second_tap_before_the_first_was_followed_wins()
    {
        // Two notifications arrive while the app is closed and the reader taps the newer one last.
        var pending = new PendingNotificationTap();
        pending.Record("/inventory");

        pending.Record("/map");

        Assert.Equal("/map", pending.TakeAtStartup());
    }

    [Fact]
    public async Task Launching_from_a_tap_opens_what_it_pointed_at()
    {
        using var context = new LaunchContext();
        context.Tapped("/inventory");

        var followed = await context.Opener.FollowTapThatLaunchedTheAppAsync();

        Assert.True(followed);
        Assert.Equal("ShowInventory", context.Navigator.LastDestination);
    }

    [Fact]
    public async Task Launching_without_a_tap_leaves_the_app_to_open_where_it_normally_would()
    {
        using var context = new LaunchContext();

        var followed = await context.Opener.FollowTapThatLaunchedTheAppAsync();

        Assert.False(followed);
        Assert.Empty(context.Navigator.Destinations);
    }

    [Fact]
    public async Task A_tap_that_leads_nowhere_still_lets_the_app_open()
    {
        // The caller falls through to its usual landing screen on false. Reporting success here would
        // leave a cold start sitting on the splash screen for good.
        using var context = new LaunchContext();
        context.Tapped("/a-screen-added-later");

        Assert.False(await context.Opener.FollowTapThatLaunchedTheAppAsync());
    }

    [Fact]
    public async Task A_notification_carrying_no_destination_leaves_the_app_to_open_normally()
    {
        using var context = new LaunchContext();
        context.Tapped(url: null);

        Assert.False(await context.Opener.FollowTapThatLaunchedTheAppAsync());
        Assert.Empty(context.Navigator.Destinations);
    }

    private sealed class LaunchContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
        private readonly PendingNotificationTap _pendingTap = new();
        private readonly Guid _ownUserId = Guid.NewGuid();

        public LaunchContext()
        {
            var chatServer = new FakeChatServer(_clock) { CallerUserId = _ownUserId };
            var users = new FakeUsersServer();

            var keyStorage = new InMemoryChatKeyStorage();
            var vectors = BrowserVectorsFile.Read();
            using (var own = ChatIdentity.FromBackup(vectors.Alice.Backup, vectors.BackupPassword)!)
            {
                keyStorage.WritePrivateKeyJwkAsync(_ownUserId, own.ExportPrivateKeyJwk()).GetAwaiter().GetResult();
            }

            var sessionStore = new SessionStore(new InMemorySessionStorage(
                new UserSession("access", "refresh", _ownUserId, "me@orbit.example", "Me")));
            var encryptionKeyProvider = new OwnEncryptionKeyProvider(
                keyStorage, new EncryptionKeyClient(new FakeEncryptionKeyServer().ToHttpClient()),
                sessionStore, NullLogger<OwnEncryptionKeyProvider>.Instance);

            var repository = new ChatRepository(_localStore, _clock);
            var chatClient = new ChatClient(chatServer.ToHttpClient());
            var usersClient = new UsersClient(users.ToHttpClient());
            var sender = new EncryptedChatMessageSender(
                repository, chatClient, new ChatDirectoryReader(chatClient, usersClient, sessionStore),
                encryptionKeyProvider, NullLogger<EncryptedChatMessageSender>.Instance);
            var synchronizer = new ChatSynchronizer(
                repository, chatClient, usersClient, sender, NullLogger<ChatSynchronizer>.Instance);

            Opener = new NotificationOpener(repository, synchronizer, usersClient, _pendingTap, Navigator);
        }

        public NotificationOpener Opener { get; }

        public RecordingScreenNavigator Navigator { get; } = new();

        /// <summary>What platform code does the moment a notification is tapped.</summary>
        public void Tapped(string? url) => _pendingTap.Record(url);

        public void Dispose() => _localStore.Dispose();
    }
}
