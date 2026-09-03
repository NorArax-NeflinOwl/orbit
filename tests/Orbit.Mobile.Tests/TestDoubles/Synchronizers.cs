using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Permissions;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// An <see cref="EverythingSynchronizer"/> for a test that has to hand one over but is not about
/// syncing - the navigation bar, which takes one only so its Reconnect button has something to try.
///
/// Every server it talks to is unreachable, which is the honest stand-in: the bar reads what is
/// remembered and asks nobody, and a sync that quietly succeeds would make a test about the bar depend
/// on five fake servers behaving.
/// </summary>
internal static class Synchronizers
{
    /// <summary>
    /// One that really does talk to the four fake servers handed in - what a test about a review needs,
    /// since answering a review queues a write and the point is whether it arrives.
    ///
    /// Chat and permissions still talk to nobody: they are not what any of those tests are about.
    /// </summary>
    public static EverythingSynchronizer Against(
        LocalStore localStore, ChatRepository chat, UserPermissions permissions, SessionStore sessionStore,
        HttpClient notes, HttpClient tasks, HttpClient calendar, HttpClient inventory)
    {
        var nobody = StubHttpMessageHandler.Unreachable().ToHttpClient();
        var gate = new SyncGate();
        var clock = TimeProvider.System;
        var chatClient = new ChatClient(nobody);
        var usersClient = new UsersClient(nobody);

        return new EverythingSynchronizer(
            new NoteSynchronizer(
                localStore, new NotesClient(notes), clock, gate,
                NullLogger<NoteSynchronizer>.Instance),
            new TaskListSynchronizer(
                localStore, new TasksClient(tasks), clock, gate,
                NullLogger<TaskListSynchronizer>.Instance),
            new CalendarEventSynchronizer(
                localStore, new CalendarClient(calendar), clock, gate,
                new PendingCalendarLinkResolver(clock, NullLogger<PendingCalendarLinkResolver>.Instance),
                NullLogger<CalendarEventSynchronizer>.Instance),
            new InventorySynchronizer(
                localStore, new InventoryClient(inventory), clock, gate,
                NullLogger<InventorySynchronizer>.Instance),
            new ChatSynchronizer(
                chat, chatClient, usersClient,
                new EncryptedChatMessageSender(
                    chat, chatClient, new ChatDirectoryReader(chatClient, usersClient, sessionStore),
                    new OwnEncryptionKeyProvider(
                        new InMemoryChatKeyStorage(), new EncryptionKeyClient(nobody), sessionStore,
                        NullLogger<OwnEncryptionKeyProvider>.Instance),
                    gate, NullLogger<EncryptedChatMessageSender>.Instance),
                NullLogger<ChatSynchronizer>.Instance),
            permissions);
    }

    public static EverythingSynchronizer AgainstNobody(
        LocalStore localStore, ChatRepository chat, UserPermissions permissions, SessionStore sessionStore)
    {
        var nobody = StubHttpMessageHandler.Unreachable().ToHttpClient();
        var gate = new SyncGate();
        var clock = TimeProvider.System;
        var chatClient = new ChatClient(nobody);
        var usersClient = new UsersClient(nobody);

        return new EverythingSynchronizer(
            new NoteSynchronizer(localStore, new NotesClient(nobody), clock, gate, NullLogger<NoteSynchronizer>.Instance),
            new TaskListSynchronizer(localStore, new TasksClient(nobody), clock, gate, NullLogger<TaskListSynchronizer>.Instance),
            new CalendarEventSynchronizer(
                localStore, new CalendarClient(nobody), clock, gate,
                new PendingCalendarLinkResolver(clock, NullLogger<PendingCalendarLinkResolver>.Instance),
                NullLogger<CalendarEventSynchronizer>.Instance),
            new InventorySynchronizer(localStore, new InventoryClient(nobody), clock, gate, NullLogger<InventorySynchronizer>.Instance),
            new ChatSynchronizer(
                chat, chatClient, usersClient,
                new EncryptedChatMessageSender(
                    chat, chatClient, new ChatDirectoryReader(chatClient, usersClient, sessionStore),
                    new OwnEncryptionKeyProvider(
                        new InMemoryChatKeyStorage(), new EncryptionKeyClient(nobody), sessionStore,
                        NullLogger<OwnEncryptionKeyProvider>.Instance),
                    gate, NullLogger<EncryptedChatMessageSender>.Instance),
                NullLogger<ChatSynchronizer>.Instance),
            permissions);
    }
}
