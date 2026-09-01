using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
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
    /// <summary>
    /// Putting a conversation away, which the web gained on 2026-09-01. One-sided and told to nobody:
    /// the other party's list has its own row and its own answer, which is why this needs no rank, no
    /// approval and no notice to anybody.
    /// </summary>
    [Fact]
    public async Task A_conversation_can_be_put_away_and_brought_back()
    {
        using var context = new ContactsContext();
        var contact = context.Server.AddContact(Guid.NewGuid(), "a-key");
        var screen = context.OpenContacts();
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.SetArchivedCommand.ExecuteAsync(Assert.Single(screen.Contacts));

        // Gone from the list somebody starts on, and the way to it is now offered.
        Assert.Empty(screen.Contacts);
        Assert.True(screen.HasArchive);

        screen.IsShowingArchive = true;
        await screen.LoadCommand.ExecuteAsync(null);
        var putAway = Assert.Single(screen.Contacts);
        Assert.Equal(contact.UserId, putAway.UserId);

        await screen.SetArchivedCommand.ExecuteAsync(putAway);

        // And back it comes, taking the archive with it: an empty one is not offered.
        Assert.False(screen.HasArchive);
        Assert.False(screen.IsShowingArchive);
    }

    /// <summary>
    /// Emptying a conversation is the reader's own side only - the server records where their history
    /// begins rather than deleting anybody's messages. This phone has to drop what it cached as well,
    /// or the words would still be here: a pull only ever adds.
    /// </summary>
    [Fact]
    public async Task Emptying_a_conversation_takes_it_off_this_phone_too()
    {
        using var context = new ContactsContext();
        // A real key: the message is sealed for them on the way out, so a made-up one fails to encrypt.
        var contact = context.Server.AddContact(Guid.NewGuid(), BrowserVectorsFile.Read().Bob.PublicKeyBase64);
        var screen = context.OpenContacts();
        await screen.LoadCommand.ExecuteAsync(null);
        var row = Assert.Single(screen.Contacts);

        await context.Conversation(row).SendCommand.ExecuteAsync("something said out loud");
        Assert.NotEmpty(await context.Repository.GetConversationAsync(contact.UserId));

        await screen.ClearHistoryCommand.ExecuteAsync(row);

        Assert.Empty(await context.Repository.GetConversationAsync(contact.UserId));
    }

    /// <summary>
    /// A group can be put away too, and the flag lives on this reader's own membership - so tidying
    /// one list cannot take the group off anybody else's, and it needs no rank at all.
    /// </summary>
    [Fact]
    public async Task A_group_can_be_put_away_and_brought_back()
    {
        using var context = new ContactsContext();
        context.Server.AddGroup("Weekend trip");
        var screen = context.OpenGroups();

        await screen.SetArchivedCommand.ExecuteAsync(Assert.Single(screen.Groups));

        Assert.Empty(screen.Groups);
        Assert.True(screen.HasArchive);

        screen.IsShowingArchive = true;
        await screen.LoadCommand.ExecuteAsync(null);
        await screen.SetArchivedCommand.ExecuteAsync(Assert.Single(screen.Groups));

        Assert.False(screen.HasArchive);
        Assert.Single(context.Server.Groups);
    }

    /// <summary>
    /// Leaving is the other thing: a member who puts a group away is still in it and still receives
    /// what is posted, and one who leaves is out of it and the rest of the group sees them go.
    /// </summary>
    [Fact]
    public async Task Leaving_a_group_takes_this_account_out_of_it()
    {
        using var context = new ContactsContext();
        context.Server.AddGroup("Weekend trip", Guid.NewGuid());
        var screen = context.OpenGroups();

        await screen.LeaveCommand.ExecuteAsync(Assert.Single(screen.Groups));

        Assert.Empty(screen.Groups);
        // The group itself is still there for whoever is left in it.
        Assert.Single(context.Server.Groups);
    }

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

    [Fact]
    public void Somebody_who_has_never_opened_chat_is_told_why_they_cannot_be_written_to()
    {
        // Reported from using it: search for a new person, open the conversation, and find "no messages
        // yet" and no way to write one. Hiding the compose box was right - there is no key to encrypt
        // for - but saying nothing about it reads as the app being broken.
        using var context = new ContactsContext();
        var screen = context.Conversation(LocalContact.ForSomebodyNotYetSpokenTo(
            Guid.NewGuid(), "nokey", "Bez klucza", publicKeyBase64: null));

        Assert.False(screen.CanCompose);
        Assert.True(screen.CannotWrite);
        Assert.Contains("Bez klucza", screen.CannotWriteReason);
    }

    [Fact]
    public void Somebody_with_a_published_key_gets_the_compose_box_and_no_explanation()
    {
        using var context = new ContactsContext();
        var screen = context.Conversation(LocalContact.ForSomebodyNotYetSpokenTo(
            Guid.NewGuid(), "bob", "Bob", BrowserVectorsFile.Read().Bob.PublicKeyBase64));

        Assert.True(screen.CanCompose);
        Assert.False(screen.CannotWrite);
    }

    /// <summary>
    /// A conversation that is still a request says so at the top, rather than letting somebody write a
    /// message and find out when the send comes back refused. Orbit.Web puts the same two sentences
    /// there; the phone reads them off the contact row it already synced instead of asking the server.
    /// </summary>
    [Fact]
    public void A_request_waiting_on_the_reader_offers_the_way_to_allow_it()
    {
        using var context = new ContactsContext();
        var screen = context.Conversation(new LocalContact
        {
            UserId = Guid.NewGuid(), DisplayName = "Bob", RequiresApprovalFromCurrentUser = true
        });

        Assert.True(screen.HasRequestNotice);
        Assert.Contains("Bob", screen.RequestNotice);
        Assert.True(screen.CanApproveRequest);
    }

    /// <summary>The other direction is a notice, not a choice - there is nothing here to approve.</summary>
    [Fact]
    public void A_request_waiting_on_the_other_party_says_so_and_offers_nothing()
    {
        using var context = new ContactsContext();
        var screen = context.Conversation(new LocalContact
        {
            UserId = Guid.NewGuid(), DisplayName = "Bob", IsPendingApprovalFromOtherParty = true
        });

        Assert.True(screen.HasRequestNotice);
        Assert.False(screen.CanApproveRequest);
    }

    [Fact]
    public void An_established_conversation_says_nothing_about_requests()
    {
        using var context = new ContactsContext();
        var screen = context.Conversation(new LocalContact { UserId = Guid.NewGuid(), DisplayName = "Bob" });

        Assert.False(screen.HasRequestNotice);
        Assert.False(screen.CanApproveRequest);
    }

    private sealed class ContactsContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
        private readonly FakeChatServer _chatServer;

        /// <summary>The server itself, for the tests that arrange what it holds.</summary>
        public FakeChatServer Server => _chatServer;
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
            _sessionStore = new SessionStore(new InMemorySessionStorage(session));
            var sessionStore = _sessionStore;
            _encryptionKeyProvider = new OwnEncryptionKeyProvider(
                keyStorage, new EncryptionKeyClient(new FakeEncryptionKeyServer().ToHttpClient()),
                sessionStore, NullLogger<OwnEncryptionKeyProvider>.Instance);

            Repository = new ChatRepository(_localStore, _clock);
            _chatClient = new ChatClient(_chatServer.ToHttpClient());
            UsersClient = new UsersClient(Users.ToHttpClient());
            var directoryReader = new ChatDirectoryReader(_chatClient, UsersClient, sessionStore);
            var sender = new EncryptedChatMessageSender(
                Repository, _chatClient, directoryReader, _encryptionKeyProvider,
                new SyncGate(), NullLogger<EncryptedChatMessageSender>.Instance);
            _synchronizer = new ChatSynchronizer(
                Repository, _chatClient, UsersClient, sender, NullLogger<ChatSynchronizer>.Instance);
        }

        private readonly SessionStore _sessionStore;

        public FakeUsersServer Users { get; }
        public UsersClient UsersClient { get; }
        public ChatRepository Repository { get; }
        public RecordingScreenNavigator Navigator { get; } = new();

        /// <summary>Taking up an offer to share something - see SharedItemAcceptance.</summary>
        public SharedItemAcceptance Acceptance => new(
            new NotesClient(_shareServer.ToHttpClient()), new TasksClient(_shareServer.ToHttpClient()),
            new CalendarClient(_shareServer.ToHttpClient()), new InventoryClient(_shareServer.ToHttpClient()));

        private readonly FakeShareServer _shareServer = new();
        public Guid StrangerUserId { get; }

        /// <summary>The conversation screen for one person, which is where the compose box lives.</summary>
        public ConversationViewModel Conversation(LocalContact contact)
        {
            var reader = new EncryptedChatMessageReader(
                Repository, _encryptionKeyProvider, _sessionStore, new Translations(new InMemoryLanguageStore()));
            var directoryReader = new ChatDirectoryReader(_chatClient, UsersClient, _sessionStore);
            var sender = new EncryptedChatMessageSender(
                Repository, _chatClient, directoryReader, _encryptionKeyProvider,
                new SyncGate(), NullLogger<EncryptedChatMessageSender>.Instance);
            var screen = new ConversationViewModel(
                reader, sender,
                new EncryptedChatMessageEditor(
                    Repository, _chatClient, directoryReader, _encryptionKeyProvider,
                    NullLogger<EncryptedChatMessageEditor>.Instance),
                new MessageForwarder(sender), Acceptance, Repository, _synchronizer, _chatClient,
                new Translations(new InMemoryLanguageStore()), Navigator, new AnnouncedLiveUpdates());
            screen.Open(contact);
            return screen;
        }

        /// <summary>The groups list, which shares every piece the contact list is built from.</summary>
        public GroupsViewModel OpenGroups()
        {
            var screen = new GroupsViewModel(
                Repository, _chatClient, _synchronizer, _encryptionKeyProvider,
                new Translations(new InMemoryLanguageStore()), UnlockedPermissions.For(_localStore), Navigator);
            screen.LoadCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            return screen;
        }

        public ContactsViewModel OpenContacts()
        {
            var screen = new ContactsViewModel(
                Repository, _chatClient, UsersClient, _synchronizer, _encryptionKeyProvider,
                new Translations(new InMemoryLanguageStore()), UnlockedPermissions.For(_localStore), Navigator,
                Connections.Online);
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
