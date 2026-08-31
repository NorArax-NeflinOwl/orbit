using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Notifications;
using Orbit.Mobile.Screens.Notifications;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.Crypto;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The in-app notification feed: what happened while the reader was elsewhere. The two things worth
/// pinning down are that a tap lands where the notification pointed, and that read and cleared stay
/// distinct - the server keeps them apart and a client that conflates them loses entries.
/// </summary>
public sealed class NotificationFeedScreenTests
{
    [Fact]
    public async Task The_feed_lists_what_the_server_holds()
    {
        using var context = new FeedContext();
        context.Server.Add("New message", $"/chat/{Guid.NewGuid()}");
        context.Server.Add("Overdue task", $"/tasks/{Guid.NewGuid()}");
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, screen.Rows.Count);
        Assert.False(screen.HasNothing);
    }

    [Fact]
    public async Task Tapping_a_notification_opens_what_it_was_about_and_records_it_read()
    {
        using var context = new FeedContext();
        var senderUserId = await context.AddKnownContactAsync("Bob");
        context.Server.Add("New message", $"/chat/{senderUserId}");
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.OpenCommand.ExecuteAsync(screen.Rows[0]);

        Assert.Equal("ShowConversation", context.Navigator.LastDestination);
        Assert.Equal($"/chat/{senderUserId}", Assert.Single(context.Server.MarkedReadAt));
    }

    [Fact]
    public async Task A_notification_pointing_somewhere_this_build_lacks_says_so_instead_of_doing_nothing()
    {
        using var context = new FeedContext();
        context.Server.Add("Something new", "/a-screen-added-later");
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.OpenCommand.ExecuteAsync(screen.Rows[0]);

        Assert.Contains("Updating", screen.Message);
        Assert.Empty(context.Navigator.Destinations);
        // Nothing was opened, so nothing may be claimed as read.
        Assert.Empty(context.Server.MarkedReadAt);
    }

    [Fact]
    public async Task A_notification_that_leads_nowhere_still_lists_and_still_reads()
    {
        // An older app against a newer server must not lose the entry - only the tap.
        using var context = new FeedContext();
        context.Server.Add("Something new", "/a-screen-added-later");
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        var row = Assert.Single(screen.Rows);
        Assert.Equal("Something new", row.Title);
        Assert.False(row.CanBeOpened);
    }

    [Fact]
    public async Task Clearing_the_feed_empties_it_without_destroying_anything()
    {
        using var context = new FeedContext();
        context.Server.Add("New message", "/map");
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.ClearCommand.ExecuteAsync(null);

        Assert.Empty(screen.Rows);
        // Cleared is not deleted: the entry is still held, and showing everything finds it again.
        await screen.ShowEverythingCommand.ExecuteAsync(null);
        Assert.Single(screen.Rows);
    }

    [Fact]
    public async Task Marking_everything_read_leaves_the_entries_where_they_are()
    {
        using var context = new FeedContext();
        context.Server.Add("New message", "/map");
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.MarkEverythingReadCommand.ExecuteAsync(null);

        // Read means "I have seen these", not "take them away" - conflating the two loses the feed.
        var row = Assert.Single(screen.Rows);
        Assert.False(row.IsUnread);
    }

    [Fact]
    public async Task An_empty_feed_says_so_rather_than_looking_broken()
    {
        using var context = new FeedContext();
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(screen.HasNothing);
        Assert.False(screen.HasMessage);
    }

    /// <summary>
    /// What replaced "the feed is out of reach": it is this phone's feed now, so it shows what it holds
    /// and says nothing alarming. The old behaviour was an empty screen and an error on a train, for a
    /// list the phone had already been told about.
    /// </summary>
    [Fact]
    public async Task Being_out_of_reach_shows_what_the_phone_already_holds()
    {
        using var context = new FeedContext();
        context.Server.Add("A task is overdue", "/tasks/1");
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        context.Server.IsUnreachable = true;
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Equal("A task is overdue", Assert.Single(screen.Rows).Title);
        Assert.False(screen.HasMessage);
    }

    /// <summary>And the actions on it are not offered, rather than offered and refused.</summary>
    [Fact]
    public void Reading_and_clearing_are_not_offered_without_a_connection()
    {
        using var context = new FeedContext();
        var screen = context.Open(Connections.Offline);

        Assert.True(screen.Connection.IsNotMet);
        Assert.NotEmpty(screen.Connection.Explanation);
    }

    [Fact]
    public async Task A_refusal_is_not_reported_as_being_offline()
    {
        // An expired session answers; saying "out of reach" sends the reader to check their wifi.
        using var context = new FeedContext();
        context.Server.RefuseEverythingWith = HttpStatusCode.Unauthorized;
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.DoesNotContain("out of reach", screen.Message);
        Assert.Contains("signing in", screen.Message);
    }

    private sealed class FeedContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
        private readonly FakeChatServer _chatServer;
        private readonly FakeUsersServer _users = new();
        private readonly ChatSynchronizer _synchronizer;
        private readonly NotificationOpener _opener;
        private readonly Guid _ownUserId = Guid.NewGuid();

        public FeedContext()
        {
            _chatServer = new FakeChatServer(_clock) { CallerUserId = _ownUserId };

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
            var chatClient = new ChatClient(_chatServer.ToHttpClient());
            var usersClient = new UsersClient(_users.ToHttpClient());
            var sender = new EncryptedChatMessageSender(
                repository, chatClient, new ChatDirectoryReader(chatClient, usersClient, sessionStore),
                encryptionKeyProvider, new SyncGate(), NullLogger<EncryptedChatMessageSender>.Instance);
            _synchronizer = new ChatSynchronizer(
                repository, chatClient, usersClient, sender, NullLogger<ChatSynchronizer>.Instance);
            _opener = new NotificationOpener(repository, _synchronizer, usersClient, new PendingNotificationTap(), Navigator);
        }

        public FakeNotificationServer Server { get; } = new();

        public RecordingScreenNavigator Navigator { get; } = new();

        public NotificationFeedViewModel Open(ConnectionRequirement? connection = null)
            => new(
                new NotificationsClient(Server.ToHttpClient()),
                new LocalNotificationRepository(_localStore),
                new NotificationSynchronizer(
                    _localStore, new NotificationsClient(Server.ToHttpClient()), TimeProvider.System,
                    new SyncGate(), NullLogger<NotificationSynchronizer>.Instance),
                _opener, new Translations(new InMemoryLanguageStore()), Navigator,
                connection ?? Connections.Online);

        public async Task<Guid> AddKnownContactAsync(string displayName)
        {
            var userId = Guid.NewGuid();
            var vectors = BrowserVectorsFile.Read();
            _chatServer.AddContact(userId, vectors.Bob.PublicKeyBase64);
            _chatServer.Contacts[^1] = _chatServer.Contacts[^1] with { DisplayName = displayName };
            _users.Add(userId, displayName, vectors.Bob.PublicKeyBase64);
            await _synchronizer.SynchroniseContactsAsync();
            return userId;
        }

        public void Dispose() => _localStore.Dispose();
    }
}
