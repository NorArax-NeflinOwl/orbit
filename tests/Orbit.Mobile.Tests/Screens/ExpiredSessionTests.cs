using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using System.Net;
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

/// <summary>
/// What a screen does when the server refuses it. These exist because of a crash, not a theory: a
/// session that had expired came back 401 on the chat list, nothing on that path caught it, and since
/// these loads are started from OnAppearing without being awaited, the unobserved failure took the whole
/// app down. Being offline was handled everywhere; being refused was not.
///
/// The app is already on its way to sign-in by then - the refresh failing clears the session and the
/// navigator watches it - so there is nothing for the screen to do about the refusal except survive it.
/// </summary>
namespace Orbit.Mobile.Tests.Screens;

public sealed class ExpiredSessionTests
{
    [Fact]
    public async Task The_chat_list_survives_a_refused_session()
    {
        using var context = new RefusedContext();
        var screen = context.Contacts();

        // Would have thrown out of the command, and on a device that is a process death.
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Empty(screen.Contacts);
        Assert.NotEqual(string.Empty, screen.Message);
    }

    [Fact]
    public async Task The_group_list_survives_a_refused_session()
    {
        using var context = new RefusedContext();
        var screen = context.Groups();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Empty(screen.Groups);
        Assert.NotEqual(string.Empty, screen.Message);
    }

    [Fact]
    public async Task A_groups_detail_screen_survives_a_refused_session()
    {
        using var context = new RefusedContext();
        var screen = context.GroupDetail(new LocalChatGroup { Id = Guid.NewGuid(), Name = "Trip" });

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.NotEqual(string.Empty, screen.Message);
    }

    private sealed class RefusedContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
        private readonly FakeUsersServer _users = new();
        private readonly FakeChatServer _server;
        private readonly ChatRepository _repository;
        private readonly ChatClient _chatClient;
        private readonly UsersClient _usersClient;
        private readonly ChatSynchronizer _synchronizer;
        private readonly OwnEncryptionKeyProvider _encryptionKeyProvider;
        private readonly SessionStore _sessionStore;

        public RefusedContext()
        {
            var ownUserId = Guid.NewGuid();
            _server = new FakeChatServer(_clock) { CallerUserId = ownUserId, RefuseEverythingWith = HttpStatusCode.Unauthorized };

            var keyStorage = new InMemoryChatKeyStorage();
            var vectors = BrowserVectorsFile.Read();
            using (var own = ChatIdentity.FromBackup(vectors.Alice.Backup, vectors.BackupPassword)!)
            {
                keyStorage.WritePrivateKeyJwkAsync(ownUserId, own.ExportPrivateKeyJwk()).GetAwaiter().GetResult();
            }

            _sessionStore = new SessionStore(new InMemorySessionStorage(
                new UserSession("access", "refresh", ownUserId, "me@orbit.example", "Me")));
            _encryptionKeyProvider = new OwnEncryptionKeyProvider(
                keyStorage, new EncryptionKeyClient(new FakeEncryptionKeyServer().ToHttpClient()),
                _sessionStore, NullLogger<OwnEncryptionKeyProvider>.Instance);

            _repository = new ChatRepository(_localStore, _clock);
            _chatClient = new ChatClient(_server.ToHttpClient());
            _usersClient = new UsersClient(_users.ToHttpClient());
            var sender = new EncryptedChatMessageSender(
                _repository, _chatClient, new ChatDirectoryReader(_chatClient, _usersClient, _sessionStore),
                _encryptionKeyProvider, new SyncGate(), NullLogger<EncryptedChatMessageSender>.Instance);
            _synchronizer = new ChatSynchronizer(
                _repository, _chatClient, _usersClient, sender, NullLogger<ChatSynchronizer>.Instance);
        }

        public RecordingScreenNavigator Navigator { get; } = new();

        public ContactsViewModel Contacts()
            => new(_repository, _chatClient, _usersClient, _synchronizer, _encryptionKeyProvider,
                new Translations(new InMemoryLanguageStore()), UnlockedPermissions.For(_localStore), Navigator);

        public GroupsViewModel Groups()
            => new(
                _repository, _chatClient, _synchronizer, _encryptionKeyProvider,
                new Translations(new InMemoryLanguageStore()), UnlockedPermissions.For(_localStore), Navigator);

        public GroupDetailViewModel GroupDetail(LocalChatGroup group)
        {
            var screen = new GroupDetailViewModel(
                _repository, _chatClient, _synchronizer, _sessionStore,
                new Translations(new InMemoryLanguageStore()), Navigator);
            screen.Open(group);
            return screen;
        }

        public void Dispose()
        {
            _server.Dispose();
            _users.Dispose();
            _localStore.Dispose();
        }
    }
}
