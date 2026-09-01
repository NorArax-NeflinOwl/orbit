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
    /// <summary>
    /// The stored task lists an opener reads to turn a notification's server id into the local one every
    /// screen is opened by - see NotificationOpener.
    /// </summary>
    public static LocalTaskListRepository TaskListsIn(LocalStore localStore)
        => new(localStore, TimeProvider.System, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());

    /// <summary>
    /// The task list half for a test that is not about task lists. Its server cannot be reached, so a
    /// path naming a list this phone has never pulled answers "not on this phone yet" rather than
    /// depending on a fake nobody in that test set up.
    /// </summary>
    public static TaskListSynchronizer NoTaskListServer(LocalStore localStore)
        => new(
            localStore, new TasksClient(StubHttpMessageHandler.Unreachable().ToHttpClient()),
            TimeProvider.System, new SyncGate(), NullLogger<TaskListSynchronizer>.Instance);

    public static NotificationOpener AgainstNobody(LocalStore localStore, IScreenNavigator navigator)
        => AgainstNobody(localStore, navigator, new PendingNotificationTap());

    /// <summary>
    /// The same, with the holder handed in - for a test about what happens to a tap that was waiting,
    /// which has to be able to record one.
    /// </summary>
    public static NotificationOpener AgainstNobody(
        LocalStore localStore, IScreenNavigator navigator, PendingNotificationTap pendingTap)
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
            usersClient, TaskListsIn(localStore), NoTaskListServer(localStore),
            pendingTap, navigator);
    }
}
