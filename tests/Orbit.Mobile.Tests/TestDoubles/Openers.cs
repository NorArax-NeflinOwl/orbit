using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Notifications;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// A <see cref="NotificationOpener"/> for a test that has to hand one over but is not about opening
/// notifications - the navigation bar, which takes one only so a banner has somewhere to go.
///
/// Every server it talks to is unreachable, which is the honest stand-in: a test about the bar should
/// not depend on five fakes behaving. What a tap actually does is covered by
/// TappedNotificationLaunchTests.
/// </summary>
internal static class Openers
{
    public static NotificationOpener AgainstNobody(LocalStore localStore, IScreenNavigator navigator)
    {
        var nobody = StubHttpMessageHandler.Unreachable().ToHttpClient();
        var clock = TimeProvider.System;
        var chat = new ChatRepository(localStore, clock);
        var chatClient = new ChatClient(nobody);
        var usersClient = new UsersClient(nobody);
        var sessionStore = new SessionStore(new InMemorySessionStorage(
            new UserSession("access", "refresh", Guid.NewGuid(), "me@orbit.example", "Me")));

        var sender = new EncryptedChatMessageSender(
            chat, chatClient, new ChatDirectoryReader(chatClient, usersClient, sessionStore),
            new OwnEncryptionKeyProvider(
                new InMemoryChatKeyStorage(), new EncryptionKeyClient(nobody), sessionStore,
                NullLogger<OwnEncryptionKeyProvider>.Instance),
            new SyncGate(), NullLogger<EncryptedChatMessageSender>.Instance);

        return new NotificationOpener(
            chat,
            new ChatSynchronizer(chat, chatClient, usersClient, sender, NullLogger<ChatSynchronizer>.Instance),
            usersClient, new PendingNotificationTap(), navigator);
    }
}
