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
            new WarehouseSynchronizer(localStore, new InventoryClient(nobody), clock, gate, NullLogger<WarehouseSynchronizer>.Instance),
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
