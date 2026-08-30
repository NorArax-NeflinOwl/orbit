using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// A <see cref="GroupHistorySharing"/> for a group-screen test that is not about handing the past over.
/// The screen holds one, so every test of it has to supply one.
/// </summary>
internal static class GroupHistory
{
    public static GroupHistorySharing SharedBy(
        ChatClient chatClient, SessionStore sessionStore, FakeUsersServer? users = null,
        InMemoryChatKeyStorage? keyStorage = null)
    {
        var usersClient = new UsersClient((users ?? new FakeUsersServer()).ToHttpClient());
        var keys = new OwnEncryptionKeyProvider(
            keyStorage ?? new InMemoryChatKeyStorage(),
            new EncryptionKeyClient(new FakeEncryptionKeyServer().ToHttpClient()), sessionStore,
            NullLogger<OwnEncryptionKeyProvider>.Instance);

        return new GroupHistorySharing(
            chatClient, keys, new ChatDirectoryReader(chatClient, usersClient, sessionStore), sessionStore);
    }
}
