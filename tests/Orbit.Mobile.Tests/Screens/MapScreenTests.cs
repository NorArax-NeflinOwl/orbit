using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Location;
using Orbit.Mobile.Screens.Location;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.Crypto;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The location screen. Its whole job is keeping two things apart that look alike: knowing where you
/// are, which is the reader's own business, and letting somebody see it, which is a decision they make
/// one person at a time.
/// </summary>
public sealed class MapScreenTests
{
    [Fact]
    public async Task Reading_a_position_records_it_without_showing_it_to_anybody()
    {
        using var context = new MapContext();
        var screen = context.Open();

        await screen.ReadMyPositionCommand.ExecuteAsync(null);

        Assert.NotNull(context.LocationServer.OwnLocation);
        // Recorded, and nobody can see it until the reader says who.
        Assert.Empty(context.LocationServer.Shares);
        Assert.Empty(screen.SharingWith);
    }

    [Fact]
    public async Task A_refused_permission_says_where_to_turn_it_on_rather_than_failing_quietly()
    {
        using var context = new MapContext();
        context.Device.Reading = new DeviceLocationResult(DeviceLocationOutcome.NotPermitted);
        var screen = context.Open();

        await screen.ReadMyPositionCommand.ExecuteAsync(null);

        Assert.Contains("Settings", screen.Message);
        Assert.Null(context.LocationServer.OwnLocation);
    }

    [Fact]
    public async Task Sharing_is_offered_only_once_there_is_a_position_to_share()
    {
        using var context = new MapContext();
        var screen = context.Open();

        await screen.StartSharingCommand.ExecuteAsync(null);

        Assert.False(screen.IsChoosingWhoToShareWith);
        Assert.Contains("Read your position first", screen.Message);
    }

    [Fact]
    public async Task Reading_a_position_puts_it_on_the_map()
    {
        using var context = new MapContext();
        var screen = context.Open();

        await screen.ReadMyPositionCommand.ExecuteAsync(null);

        var point = Assert.Single(screen.Points);
        Assert.True(point.IsMine);
        Assert.Equal("You", point.Label);
        Assert.Equal(52.2297, point.Latitude, precision: 4);
    }

    [Fact]
    public async Task A_position_somebody_shares_is_drawn_next_to_the_readers_own()
    {
        using var context = new MapContext();
        await context.SomebodySharesTheirPositionAsync("Bob");
        var screen = context.Open();

        await screen.ReadMyPositionCommand.ExecuteAsync(null);
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, screen.Points.Count);
        // The reader's own comes first, which is what the map centres on.
        Assert.True(screen.Points[0].IsMine);
        Assert.Equal("Bob", screen.Points[1].Label);
    }

    [Fact]
    public async Task A_position_that_cannot_be_opened_is_listed_but_not_drawn()
    {
        // There are no coordinates to draw. Dropping it from the list as well would hide that somebody
        // is sharing at all, which is the part worth knowing.
        using var context = new MapContext();
        context.SomebodySharesSomethingUnreadable("Bob");
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Single(screen.SharedWithMe);
        Assert.Empty(screen.Points);
    }

    [Fact]
    public async Task A_refusal_is_not_reported_as_being_offline()
    {
        // Found on a device: a stale session made the server answer 404, and the screen called it "out
        // of reach" - which sends somebody to check their signal instead of their session.
        using var context = new MapContext();
        context.LocationServer.RefuseEverythingWith = System.Net.HttpStatusCode.NotFound;
        var screen = context.Open();

        await screen.ReadMyPositionCommand.ExecuteAsync(null);

        Assert.DoesNotContain("out of reach", screen.Message);
        Assert.Contains("signing in", screen.Message);
    }

    [Fact]
    public async Task Being_genuinely_unreachable_still_says_so()
    {
        using var context = new MapContext();
        context.LocationServer.IsUnreachable = true;
        var screen = context.Open();

        await screen.ReadMyPositionCommand.ExecuteAsync(null);

        Assert.Contains("out of reach", screen.Message);
    }

    [Fact]
    public async Task Somebody_shared_with_shows_up_in_who_can_see_you_and_can_be_stopped()
    {
        using var context = new MapContext();
        var bob = await context.AddContactAsync("Bob");
        var screen = context.Open();
        await screen.ReadMyPositionCommand.ExecuteAsync(null);

        await screen.StartSharingCommand.ExecuteAsync(null);
        await screen.ShareWithCommand.ExecuteAsync(screen.Candidates.Single(candidate => candidate.UserId == bob));

        var sharedWith = Assert.Single(screen.SharingWith);
        Assert.Equal("Bob", sharedWith.DisplayName);

        await screen.StopSharingWithCommand.ExecuteAsync(sharedWith);

        Assert.Empty(screen.SharingWith);
        Assert.Empty(context.LocationServer.Shares);
    }

    private sealed class MapContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
        private readonly FakeUsersServer _users = new();
        private readonly FakeChatServer _chatServer;
        private readonly ChatRepository _repository;
        private readonly ChatSynchronizer _synchronizer;
        private readonly SharedLocations _sharedLocations;
        private readonly UsersClient _usersClient;
        private readonly Guid _ownUserId = Guid.NewGuid();

        public MapContext()
        {
            _chatServer = new FakeChatServer(_clock) { CallerUserId = _ownUserId };
            LocationServer = new FakeLocationServer(_clock) { CallerUserId = _ownUserId };

            var keyStorage = new InMemoryChatKeyStorage();
            var vectors = BrowserVectorsFile.Read();
            using (var own = ChatIdentity.FromBackup(vectors.Alice.Backup, vectors.BackupPassword)!)
            {
                keyStorage.WritePrivateKeyJwkAsync(_ownUserId, own.ExportPrivateKeyJwk()).GetAwaiter().GetResult();
            }

            var sessionStore = new SessionStore(new InMemorySessionStorage(
                new UserSession("access", "refresh", _ownUserId, "me@orbit.example", "Me")));
            var encryptionKeyProvider = new OwnEncryptionKeyProvider(
                keyStorage, new EncryptionKeyClient(new FakeEncryptionKeyServer().ToHttpClient()),
                sessionStore, NullLogger<OwnEncryptionKeyProvider>.Instance);

            _repository = new ChatRepository(_localStore, _clock);
            var chatClient = new ChatClient(_chatServer.ToHttpClient());
            _usersClient = new UsersClient(_users.ToHttpClient());
            var sender = new EncryptedChatMessageSender(
                _repository, chatClient, new ChatDirectoryReader(chatClient, _usersClient, sessionStore),
                encryptionKeyProvider, NullLogger<EncryptedChatMessageSender>.Instance);
            _synchronizer = new ChatSynchronizer(
                _repository, chatClient, _usersClient, sender, NullLogger<ChatSynchronizer>.Instance);
            LocationClient = new LocationClient(LocationServer.ToHttpClient());
            _sharedLocations = new SharedLocations(
                LocationClient, _usersClient, encryptionKeyProvider, NullLogger<SharedLocations>.Instance);
        }

        public FakeLocationServer LocationServer { get; }
        public LocationClient LocationClient { get; }
        public FixedDeviceLocation Device { get; } = new();
        public RecordingScreenNavigator Navigator { get; } = new();

        /// <summary>Somebody with a published key, so a position can actually be sealed for them.</summary>
        public async Task<Guid> AddContactAsync(string displayName)
        {
            var userId = Guid.NewGuid();
            var vectors = BrowserVectorsFile.Read();
            _chatServer.AddContact(userId, vectors.Bob.PublicKeyBase64);
            _chatServer.Contacts[^1] = _chatServer.Contacts[^1] with { DisplayName = displayName };
            _users.Add(userId, displayName, vectors.Bob.PublicKeyBase64);
            await _synchronizer.SynchroniseContactsAsync();
            return userId;
        }

        /// <summary>Somebody else sharing a position sealed for the reader, as the server would hold it.</summary>
        public async Task SomebodySharesTheirPositionAsync(string displayName)
        {
            var sharerUserId = await AddContactAsync(displayName);
            var vectors = BrowserVectorsFile.Read();
            using var theirIdentity = ChatIdentity.FromBackup(vectors.Bob.Backup, vectors.BackupPassword)!;
            using var own = ChatIdentity.FromBackup(vectors.Alice.Backup, vectors.BackupPassword)!;

            var position = new SharedPosition(50.0647, 19.9450, "Kraków", DateTimeOffset.Parse("2026-08-26T09:00:00Z"));
            var sealedPosition = theirIdentity.Encrypt(own.PublicKeyBase64, position.ToJson());
            LocationServer.AddIncomingShare(sharerUserId, sealedPosition.CiphertextBase64, sealedPosition.NonceBase64);
        }

        public void SomebodySharesSomethingUnreadable(string displayName)
        {
            var sharerUserId = Guid.NewGuid();
            _users.Add(sharerUserId, displayName, BrowserVectorsFile.Read().Bob.PublicKeyBase64);
            LocationServer.AddIncomingShare(sharerUserId, "AAAAAAAAAAAAAAAAAAAAAA==", "AAAAAAAAAAAAAAAA");
        }

        public MapViewModel Open()
            => new(Device, LocationClient, _sharedLocations, _usersClient, _repository, _synchronizer, Navigator);

        public void Dispose()
        {
            _chatServer.Dispose();
            LocationServer.Dispose();
            _users.Dispose();
            _localStore.Dispose();
        }
    }
}
