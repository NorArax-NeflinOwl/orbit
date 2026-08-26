using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Screens.Chat;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.Crypto;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The chat list, and the thing it could not do until now: reach somebody this phone has never spoken
/// to. Without the search there is no way to start a conversation from the device at all - the list
/// holds only people the server already counts as contacts, and it only counts them once a message has
/// been sent.
/// </summary>
public sealed class ContactsScreenTests
{
    [Fact]
    public async Task Somebody_never_spoken_to_can_be_found_and_opened()
    {
        using var context = new ContactsContext();
        context.Users.Add(context.StrangerUserId, "Bogdan", publicKeyBase64: "a-key");

        var screen = context.OpenContacts();
        screen.SearchQuery = "bogdan";
        await screen.SearchCommand.ExecuteAsync(null);

        Assert.Equal("Bogdan", screen.FoundPerson!.DisplayName);
        Assert.True(screen.HasFoundSomebody);

        screen.OpenConversationCommand.Execute(screen.FoundPerson);
        Assert.Equal("ShowConversation", context.Navigator.LastDestination);
    }

    [Fact]
    public async Task Finding_somebody_does_not_put_them_in_the_chat_list()
    {
        // The server decides who is a contact, and it decides that when a message is sent. Writing them
        // down here would show them in the list before any conversation existed - and the next refresh,
        // which replaces the list wholesale, would drop them again.
        using var context = new ContactsContext();
        context.Users.Add(context.StrangerUserId, "Bogdan", publicKeyBase64: "a-key");

        var screen = context.OpenContacts();
        screen.SearchQuery = "bogdan";
        await screen.SearchCommand.ExecuteAsync(null);

        Assert.Empty(await context.Repository.GetContactsAsync());
        Assert.Empty(screen.Contacts);
    }

    [Fact]
    public async Task A_search_that_matches_nobody_says_the_address_has_to_be_exact()
    {
        // The server matches exactly, on purpose, so the search cannot be used to enumerate accounts.
        // A partial name finding nothing would otherwise read as "they are not on Orbit".
        using var context = new ContactsContext();
        context.Users.Add(context.StrangerUserId, "Bogdan", publicKeyBase64: "a-key");

        var screen = context.OpenContacts();
        screen.SearchQuery = "bog";
        await screen.SearchCommand.ExecuteAsync(null);

        Assert.Null(screen.FoundPerson);
        Assert.Contains("exactly", screen.Message);
    }

    [Fact]
    public async Task Searching_with_no_connection_says_so_rather_than_reporting_nobody()
    {
        using var context = new ContactsContext();
        context.Users.Add(context.StrangerUserId, "Bogdan", publicKeyBase64: "a-key");
        context.Users.IsUnreachable = true;

        var screen = context.OpenContacts();
        screen.SearchQuery = "bogdan";
        await screen.SearchCommand.ExecuteAsync(null);

        Assert.Null(screen.FoundPerson);
        Assert.Contains("connection", screen.Message);
    }

    [Fact]
    public async Task Opening_a_conversation_clears_the_search_behind_it()
    {
        using var context = new ContactsContext();
        context.Users.Add(context.StrangerUserId, "Bogdan", publicKeyBase64: "a-key");

        var screen = context.OpenContacts();
        screen.SearchQuery = "bogdan";
        await screen.SearchCommand.ExecuteAsync(null);
        screen.OpenConversationCommand.Execute(screen.FoundPerson);

        // Coming back should show the chat list, not the result of a search made minutes ago.
        Assert.Null(screen.FoundPerson);
        Assert.Equal(string.Empty, screen.SearchQuery);
    }

    private sealed class ContactsContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
        private readonly FakeChatServer _chatServer;
        private readonly OwnEncryptionKeyProvider _encryptionKeyProvider;
        private readonly ChatSynchronizer _synchronizer;
        private readonly ChatClient _chatClient;

        public ContactsContext()
        {
            _chatServer = new FakeChatServer(_clock);
            Users = new FakeUsersServer();

            var ownUserId = Guid.NewGuid();
            StrangerUserId = Guid.NewGuid();
            _chatServer.CallerUserId = ownUserId;
            Users.SearcherUserId = ownUserId;

            var keyStorage = new InMemoryChatKeyStorage();
            var vectors = BrowserVectorsFile.Read();
            using (var own = ChatIdentity.FromBackup(vectors.Alice.Backup, vectors.BackupPassword)!)
            {
                keyStorage.WritePrivateKeyJwkAsync(ownUserId, own.ExportPrivateKeyJwk()).GetAwaiter().GetResult();
            }

            var session = new UserSession("access", "refresh", ownUserId, "me@orbit.example", "Me");
            var sessionStore = new SessionStore(new InMemorySessionStorage(session));
            _encryptionKeyProvider = new OwnEncryptionKeyProvider(
                keyStorage, new EncryptionKeyClient(new FakeEncryptionKeyServer().ToHttpClient()),
                sessionStore, NullLogger<OwnEncryptionKeyProvider>.Instance);

            Repository = new ChatRepository(_localStore, _clock);
            _chatClient = new ChatClient(_chatServer.ToHttpClient());
            UsersClient = new UsersClient(Users.ToHttpClient());
            var directoryReader = new ChatDirectoryReader(_chatClient, UsersClient, sessionStore);
            var sender = new EncryptedChatMessageSender(
                Repository, _chatClient, directoryReader, _encryptionKeyProvider,
                NullLogger<EncryptedChatMessageSender>.Instance);
            _synchronizer = new ChatSynchronizer(
                Repository, _chatClient, UsersClient, sender, NullLogger<ChatSynchronizer>.Instance);
        }

        public FakeUsersServer Users { get; }
        public UsersClient UsersClient { get; }
        public ChatRepository Repository { get; }
        public RecordingScreenNavigator Navigator { get; } = new();
        public Guid StrangerUserId { get; }

        public ContactsViewModel OpenContacts()
        {
            var screen = new ContactsViewModel(
                Repository, _chatClient, UsersClient, _synchronizer, _encryptionKeyProvider, Navigator);
            screen.LoadCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            return screen;
        }

        public void Dispose()
        {
            _chatServer.Dispose();
            Users.Dispose();
            _localStore.Dispose();
        }
    }
}
