using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Screens.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.Crypto;
using Orbit.Mobile.Tests.TestDoubles;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// A signed-in phone with a chat key, a local database, and a server it can sometimes reach. The keys
/// are the committed browser vectors, so what these tests encrypt is interoperable by construction.
/// </summary>
internal sealed class ChatContext : IDisposable
{
    private readonly LocalStore _localStore = new();

    public ChatContext()
    {
        Clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
        Server = new FakeChatServer(Clock);
        Users = new FakeUsersServer();

        var vectors = BrowserVectorsFile.Read();
        OwnUserId = Guid.NewGuid();
        OtherUserId = Guid.NewGuid();
        OtherIdentity = ChatIdentity.FromBackup(vectors.Bob.Backup, vectors.BackupPassword)!;
        OtherPublicKeyBase64 = vectors.Bob.PublicKeyBase64;

        // A third party, for the group tests. Generated rather than taken from the vectors, which carry
        // two: interoperability is what the vectors are for, and this one only has to be a real key.
        ThirdUserId = Guid.NewGuid();
        ThirdIdentity = ChatIdentity.Create();
        ThirdPublicKeyBase64 = ThirdIdentity.PublicKeyBase64;

        var keyStorage = new InMemoryChatKeyStorage();
        using (var own = ChatIdentity.FromBackup(vectors.Alice.Backup, vectors.BackupPassword)!)
        {
            keyStorage.WritePrivateKeyJwkAsync(OwnUserId, own.ExportPrivateKeyJwk()).GetAwaiter().GetResult();
            OwnPublicKeyBase64 = own.PublicKeyBase64;
        }

        Server.CallerUserId = OwnUserId;
        var session = new UserSession("access", "refresh", OwnUserId, "me@orbit.example", "Me");
        var sessionStore = new SessionStore(new InMemorySessionStorage(session));
        var encryptionKeyProvider = new OwnEncryptionKeyProvider(
            keyStorage, new EncryptionKeyClient(new FakeEncryptionKeyServer().ToHttpClient()),
            sessionStore, NullLogger<OwnEncryptionKeyProvider>.Instance);

        Repository = new ChatRepository(_localStore, Clock);
        ChatClient = new ChatClient(Server.ToHttpClient());
        var usersClient = new UsersClient(Users.ToHttpClient());
        var directoryReader = new ChatDirectoryReader(ChatClient, usersClient, sessionStore);
        Sender = new EncryptedChatMessageSender(
            Repository, ChatClient, directoryReader, encryptionKeyProvider,
            new SyncGate(), NullLogger<EncryptedChatMessageSender>.Instance);
        Editor = new EncryptedChatMessageEditor(
            Repository, ChatClient, directoryReader, encryptionKeyProvider,
            NullLogger<EncryptedChatMessageEditor>.Instance);
        Forwarder = new MessageForwarder(Sender);
        Reader = new EncryptedChatMessageReader(
            Repository, encryptionKeyProvider, sessionStore, new Translations(new InMemoryLanguageStore()));
        Synchronizer = new ChatSynchronizer(
            Repository, ChatClient, usersClient, Sender, NullLogger<ChatSynchronizer>.Instance);
    }

    public FakeTimeProvider Clock { get; }
    public FakeChatServer Server { get; }
    public FakeUsersServer Users { get; }
    public Guid OwnUserId { get; }
    public Guid OtherUserId { get; }
    public Guid ThirdUserId { get; }
    public string OwnPublicKeyBase64 { get; }
    public string OtherPublicKeyBase64 { get; }
    public string ThirdPublicKeyBase64 { get; }
    public ChatIdentity OtherIdentity { get; }
    public ChatIdentity ThirdIdentity { get; }
    public ChatRepository Repository { get; }
    public ChatClient ChatClient { get; }
    public EncryptedChatMessageSender Sender { get; }
    public EncryptedChatMessageEditor Editor { get; }
    public MessageForwarder Forwarder { get; }
    public EncryptedChatMessageReader Reader { get; }
    public ChatSynchronizer Synchronizer { get; }

    /// <summary>Publishes the other party's key, without which nothing can be encrypted for them.</summary>
    public void GiveTheOtherPartyAPublishedKey() => Server.AddContact(OtherUserId, OtherPublicKeyBase64);

    /// <summary>
    /// Both other people, as accounts that can be looked up by id. Group members are reached that way
    /// rather than through the contact list, which only holds people the user has spoken to.
    /// </summary>
    public void PublishGroupMemberKeys()
    {
        Users.Add(OtherUserId, "Bob", OtherPublicKeyBase64);
        Users.Add(ThirdUserId, "Carol", ThirdPublicKeyBase64);
        Users.Add(OwnUserId, "Me", OwnPublicKeyBase64);
    }

    /// <summary>Reads a message the way its recipient would - proof it is really encrypted for them.</summary>
    public string? OpenAsTheOtherParty(Orbit.Contracts.Chat.ChatMessageDto message)
        => OtherIdentity.Decrypt(OwnPublicKeyBase64, new EncryptedText(message.CiphertextBase64, message.NonceBase64));

    /// <inheritdoc cref="OpenAsTheOtherParty"/>
    public string? OpenAsTheThirdParty(Orbit.Contracts.Chat.ChatMessageDto message)
        => ThirdIdentity.Decrypt(OwnPublicKeyBase64, new EncryptedText(message.CiphertextBase64, message.NonceBase64));

    /// <summary>What is still waiting to go out, which after a successful flush should be nothing.</summary>
    public Task<IReadOnlyList<OutgoingChatMessage>> ReadQueuedAsync()
        => Repository.GetQueuedAsync(CancellationToken.None);

    public Task<IReadOnlyList<ReadableChatMessage>> ReadConversationAsync(DateTimeOffset? theyReadUpToUtc = null)
        => Reader.ReadAsync(OtherUserId, OtherPublicKeyBase64, theyReadUpToUtc, CancellationToken.None);

    /// <summary>
    /// The conversation screen for the other party, built on the same pieces the tests use directly -
    /// so what a test sets up through them is what the screen then reads.
    /// </summary>
    public ConversationViewModel Conversation()
    {
        var screen = new ConversationViewModel(
            Reader, Sender, Editor, Forwarder,
            // Accepting a shared item needs four clients this screen never reaches in these tests: what
            // is under test is the compose box, not what a share offer does when it is taken up.
            new SharedItemAcceptance(
                new NotesClient(Server.ToHttpClient()), new TasksClient(Server.ToHttpClient()),
                new CalendarClient(Server.ToHttpClient()), new InventoryClient(Server.ToHttpClient())),
            Repository, Synchronizer, ChatClient,
            new Translations(new InMemoryLanguageStore()), new RecordingScreenNavigator());

        screen.Open(LocalContact.ForSomebodyNotYetSpokenTo(
            OtherUserId, "bob", "Bob", OtherPublicKeyBase64));
        return screen;
    }

    public void Dispose()
    {
        OtherIdentity.Dispose();
        ThirdIdentity.Dispose();
        Server.Dispose();
        Users.Dispose();
        _localStore.Dispose();
    }
}
