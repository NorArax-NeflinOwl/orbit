using Orbit.Mobile.Api;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Sharing;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// A <see cref="SharePanel"/> for an editor test that is not about sharing. Every editor holds one, so
/// every editor test has to hand one over.
/// </summary>
internal static class ShareTestPanel
{
    public static SharePanel For(
        LocalStore localStore, ChatRepository chatRepository, FakeShareServer? shareServer = null,
        FakeChatServer? chatServer = null)
    {
        var shares = (shareServer ?? new FakeShareServer()).ToHttpClient();

        return new SharePanel(
            chatRepository,
            new SharedItemSharing(
                new NotesClient(shares), new TasksClient(shares), new CalendarClient(shares),
                new InventoryClient(shares), Sender(chatRepository, chatServer)),
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
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EncryptedChatMessageSender>.Instance);
    }
}
