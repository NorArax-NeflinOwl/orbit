using Orbit.Mobile.Api;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Sharing;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// A <see cref="SharePanel"/> for an editor test that is not about sharing. Every editor holds one, so
/// every editor test has to hand one over.
/// </summary>
internal static class ShareTestPanel
{
    public static SharePanel For(
        LocalStore localStore, ChatRepository chatRepository, FakeShareServer? shareServer = null,
        FakeChatServer? chatServer = null, FakePublicShareServer? linkServer = null)
    {
        var shares = (shareServer ?? new FakeShareServer()).ToHttpClient();
        var chat = (chatServer ?? new FakeChatServer(TimeProvider.System)).ToHttpClient();
        var sender = Sender(chatRepository, chatServer);

        return new SharePanel(
            chatRepository,
            // The panel refreshes the contact list as it opens, so even an editor test that never looks
            // at sharing needs one that answers rather than throwing.
            new ChatSynchronizer(
                chatRepository, new ChatClient(chat), new UsersClient(new FakeUsersServer().ToHttpClient()),
                sender, Microsoft.Extensions.Logging.Abstractions.NullLogger<ChatSynchronizer>.Instance),
            new SharedItemSharing(
                new NotesClient(shares), new TasksClient(shares), new CalendarClient(shares),
                new InventoryClient(shares), sender),
            new PublicShareClient((linkServer ?? new FakePublicShareServer()).ToHttpClient()),
            UnlockedPermissions.For(localStore),
            new Translations(new InMemoryLanguageStore()));
    }

    private static EncryptedChatMessageSender Sender(ChatRepository chatRepository, FakeChatServer? chatServer)
    {
        var server = chatServer ?? new FakeChatServer(TimeProvider.System);
        var chatClient = new ChatClient(server.ToHttpClient());
        var sessionStore = new Orbit.Mobile.Authentication.SessionStore(new InMemorySessionStorage(
            new Orbit.Mobile.Authentication.UserSession(
                "access", "refresh", server.CallerUserId, "me@orbit.example", "Me")));

        return new EncryptedChatMessageSender(
            chatRepository, chatClient,
            new ChatDirectoryReader(chatClient, new UsersClient(new FakeUsersServer().ToHttpClient()), sessionStore),
            new Orbit.Mobile.Crypto.OwnEncryptionKeyProvider(
                new InMemoryChatKeyStorage(),
                new EncryptionKeyClient(new FakeEncryptionKeyServer().ToHttpClient()), sessionStore,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Orbit.Mobile.Crypto.OwnEncryptionKeyProvider>.Instance),
            new Orbit.Mobile.Sync.SyncGate(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EncryptedChatMessageSender>.Instance);
    }
}
