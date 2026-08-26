using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
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

        var vectors = BrowserVectorsFile.Read();
        OwnUserId = Guid.NewGuid();
        OtherUserId = Guid.NewGuid();
        OtherIdentity = ChatIdentity.FromBackup(vectors.Bob.Backup, vectors.BackupPassword)!;
        OtherPublicKeyBase64 = vectors.Bob.PublicKeyBase64;

        var keyStorage = new InMemoryChatKeyStorage();
        using (var own = ChatIdentity.FromBackup(vectors.Alice.Backup, vectors.BackupPassword)!)
        {
            keyStorage.WritePrivateKeyJwkAsync(OwnUserId, own.ExportPrivateKeyJwk()).GetAwaiter().GetResult();
            OwnPublicKeyBase64 = own.PublicKeyBase64;
        }

        var session = new UserSession("access", "refresh", OwnUserId, "me@orbit.example", "Me");
        var encryptionKeyProvider = new OwnEncryptionKeyProvider(
            keyStorage, new EncryptionKeyClient(new FakeEncryptionKeyServer().ToHttpClient()),
            new SessionStore(new InMemorySessionStorage(session)),
            NullLogger<OwnEncryptionKeyProvider>.Instance);

        Repository = new ChatRepository(_localStore, Clock);
        var chatClient = new ChatClient(Server.ToHttpClient());
        Sender = new EncryptedChatMessageSender(
            Repository, chatClient, encryptionKeyProvider, NullLogger<EncryptedChatMessageSender>.Instance);
        Reader = new EncryptedChatMessageReader(Repository, encryptionKeyProvider);
        Synchronizer = new ChatSynchronizer(Repository, chatClient, Sender, NullLogger<ChatSynchronizer>.Instance);
    }

    public FakeTimeProvider Clock { get; }
    public FakeChatServer Server { get; }
    public Guid OwnUserId { get; }
    public Guid OtherUserId { get; }
    public string OwnPublicKeyBase64 { get; }
    public string OtherPublicKeyBase64 { get; }
    public ChatIdentity OtherIdentity { get; }
    public ChatRepository Repository { get; }
    public EncryptedChatMessageSender Sender { get; }
    public EncryptedChatMessageReader Reader { get; }
    public ChatSynchronizer Synchronizer { get; }

    /// <summary>Publishes the other party's key, without which nothing can be encrypted for them.</summary>
    public void GiveTheOtherPartyAPublishedKey() => Server.AddContact(OtherUserId, OtherPublicKeyBase64);

    /// <summary>Reads a message the way its recipient would - proof it is really encrypted for them.</summary>
    public string? OpenAsTheOtherParty(Orbit.Contracts.Chat.ChatMessageDto message)
        => OtherIdentity.Decrypt(OwnPublicKeyBase64, new EncryptedText(message.CiphertextBase64, message.NonceBase64));

    public Task<IReadOnlyList<ReadableChatMessage>> ReadConversationAsync()
        => Reader.ReadAsync(OtherUserId, OtherPublicKeyBase64, CancellationToken.None);

    public void Dispose()
    {
        OtherIdentity.Dispose();
        Server.Dispose();
        _localStore.Dispose();
    }
}
