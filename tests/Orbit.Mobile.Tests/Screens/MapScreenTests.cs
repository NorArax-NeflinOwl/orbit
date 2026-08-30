using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Google;
using Orbit.Localization;
using Orbit.Mobile.Localization;
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

        await screen.ShareOnceCommand.ExecuteAsync(null);

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
    /// <summary>
    /// A shared position says when it was taken on the reader's own clock, not in the UTC it is stored
    /// in. The map was the second screen handing a raw value to XAML to format, after the two
    /// conversation pages - see MessageTimestampTests for the same bug and the same fix.
    /// </summary>
    [Fact]
    public async Task A_shared_position_says_when_it_was_taken_on_the_readers_clock()
    {
        using var context = new MapContext();
        await context.SomebodySharesTheirPositionAsync("Bob");
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        var taken = DateTimeOffset.Parse("2026-08-26T09:00:00Z");
        Assert.Equal(
            taken.ToLocalTime().ToString("g", CultureInfo.GetCultureInfo("en-US")),
            Assert.Single(screen.SharedWithMe).RecordedAt);
    }

    /// <summary>
    /// And in the reader's language. Asserted by shape so the test says nothing about which timezone it
    /// runs in: Polish writes the hour on a 24-hour clock, so an AM or PM means the phone's culture won.
    /// </summary>
    [Fact]
    public async Task A_polish_reader_is_told_when_it_was_taken_in_polish()
    {
        using var context = new MapContext();
        await context.SomebodySharesTheirPositionAsync("Bob");
        var screen = context.Open(AppLanguage.Polish);

        await screen.LoadCommand.ExecuteAsync(null);

        var recordedAt = Assert.Single(screen.SharedWithMe).RecordedAt;
        Assert.DoesNotContain("AM", recordedAt);
        Assert.DoesNotContain("PM", recordedAt);
    }

    /// <summary>
    /// A position this device cannot open has no reading to stamp, so the line is left off rather than
    /// standing empty under the sharer's name.
    /// </summary>
    [Fact]
    public async Task A_position_that_cannot_be_opened_is_stamped_with_nothing()
    {
        using var context = new MapContext();
        context.SomebodySharesSomethingUnreadable("Bob");
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.False(Assert.Single(screen.SharedWithMe).HasRecordedAt);
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

        await screen.ShareOnceCommand.ExecuteAsync(null);
        await screen.ShareWithCommand.ExecuteAsync(screen.Candidates.Single(candidate => candidate.UserId == bob));

        var sharedWith = Assert.Single(screen.SharingWith);
        Assert.Equal("Bob", sharedWith.DisplayName);

        await screen.StopSharingWithCommand.ExecuteAsync(sharedWith);

        Assert.Empty(screen.SharingWith);
        Assert.Empty(context.LocationServer.Shares);
    }

    /// <summary>
    /// Orbit.Web offers "send once" and "keep sharing"; the phone offered only the first, which is the
    /// wrong half to be missing - a phone is the thing that moves. What the server is told is the only
    /// difference, and it is the difference between a point and a trail.
    /// </summary>
    [Fact]
    public async Task Sharing_once_and_sharing_live_are_told_apart()
    {
        using var context = new MapContext();
        var bob = await context.AddContactAsync("Bob");
        var screen = context.Open();
        await screen.ReadMyPositionCommand.ExecuteAsync(null);

        await screen.KeepSharingCommand.ExecuteAsync(null);
        await screen.ShareWithCommand.ExecuteAsync(screen.Candidates.Single(candidate => candidate.UserId == bob));

        Assert.True(Assert.Single(context.LocationServer.Shares).IsContinuous);
        Assert.True(Assert.Single(screen.SharingWith).IsContinuous);
    }

    [Fact]
    public async Task A_one_off_share_says_it_is_one_off()
    {
        using var context = new MapContext();
        var bob = await context.AddContactAsync("Bob");
        var screen = context.Open();
        await screen.ReadMyPositionCommand.ExecuteAsync(null);

        await screen.ShareOnceCommand.ExecuteAsync(null);
        await screen.ShareWithCommand.ExecuteAsync(screen.Candidates.Single(candidate => candidate.UserId == bob));

        Assert.False(Assert.Single(context.LocationServer.Shares).IsContinuous);
    }

    /// <summary>
    /// Whichever button was pressed last is what the next share is, so choosing "keep sharing" and then
    /// changing your mind does not quietly leave a live share behind.
    /// </summary>
    [Fact]
    public async Task Choosing_the_other_button_changes_what_the_next_share_is()
    {
        using var context = new MapContext();
        var bob = await context.AddContactAsync("Bob");
        var screen = context.Open();
        await screen.ReadMyPositionCommand.ExecuteAsync(null);

        await screen.KeepSharingCommand.ExecuteAsync(null);
        await screen.ShareOnceCommand.ExecuteAsync(null);
        await screen.ShareWithCommand.ExecuteAsync(screen.Candidates.Single(candidate => candidate.UserId == bob));

        Assert.False(Assert.Single(context.LocationServer.Shares).IsContinuous);
    }

    /// <summary>
    /// A live share is a promise to keep sending, so the screen reads the device again and sends where
    /// the reader is now - not the point they were at when they pressed the button.
    /// </summary>
    [Fact]
    public async Task A_live_share_sends_where_the_reader_is_now()
    {
        using var context = new MapContext();
        var bob = await context.AddContactAsync("Bob");
        var screen = context.Open();
        await screen.ReadMyPositionCommand.ExecuteAsync(null);
        await screen.KeepSharingCommand.ExecuteAsync(null);
        await screen.ShareWithCommand.ExecuteAsync(screen.Candidates.Single(candidate => candidate.UserId == bob));

        context.Device.Reading = new DeviceLocationResult(DeviceLocationOutcome.Found, 51.1079, 17.0385, "Wroclaw");
        await screen.SendLiveSharesAgainAsync(CancellationToken.None);

        Assert.Equal(51.1079, context.LocationServer.OwnLocation?.Latitude);
    }

    /// <summary>Nothing is sent for a share that was only ever a point - that is what makes it one-off.</summary>
    [Fact]
    public async Task A_one_off_share_is_not_sent_again()
    {
        using var context = new MapContext();
        var bob = await context.AddContactAsync("Bob");
        var screen = context.Open();
        await screen.ReadMyPositionCommand.ExecuteAsync(null);
        await screen.ShareOnceCommand.ExecuteAsync(null);
        await screen.ShareWithCommand.ExecuteAsync(screen.Candidates.Single(candidate => candidate.UserId == bob));

        context.Device.Reading = new DeviceLocationResult(DeviceLocationOutcome.Found, 51.1079, 17.0385, "Wroclaw");
        await screen.SendLiveSharesAgainAsync(CancellationToken.None);

        // Still where they were when they shared it.
        Assert.Equal(52.2297, context.LocationServer.OwnLocation?.Latitude);
    }

    /// <summary>
    /// A reading that fails is a minute skipped, not a share ended: the next tick tries again, and the
    /// last position anybody was sent is still the last one that was true.
    /// </summary>
    [Fact]
    public async Task A_reading_that_fails_mid_share_leaves_the_share_standing()
    {
        using var context = new MapContext();
        var bob = await context.AddContactAsync("Bob");
        var screen = context.Open();
        await screen.ReadMyPositionCommand.ExecuteAsync(null);
        await screen.KeepSharingCommand.ExecuteAsync(null);
        await screen.ShareWithCommand.ExecuteAsync(screen.Candidates.Single(candidate => candidate.UserId == bob));

        context.Device.Reading = new DeviceLocationResult(DeviceLocationOutcome.Unavailable);
        await screen.SendLiveSharesAgainAsync(CancellationToken.None);

        Assert.True(Assert.Single(context.LocationServer.Shares).IsContinuous);
        Assert.Equal(52.2297, context.LocationServer.OwnLocation?.Latitude);
    }

    /// <summary>
    /// Handing a position to Google is offered on the same terms Orbit.Web offers it on - a confirmed
    /// email address or a connected Google account - so a reader meets the same rule on both clients.
    /// </summary>
    [Fact]
    public async Task A_recorded_position_can_be_opened_in_Google_Maps()
    {
        using var context = new MapContext();
        context.Users.Account = context.Users.Account with { IsEmailVerified = true };
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.ReadMyPositionCommand.ExecuteAsync(null);

        Assert.True(screen.CanOpenOwnPositionInGoogleMaps);
        Assert.Equal(
            "https://www.google.com/maps/search/?api=1&query=52.2297,21.0122",
            screen.OwnPositionInGoogleMapsUrl);
    }

    /// <summary>
    /// An account nobody has stood behind is not offered the hand-off, even with a position to point
    /// at. What is withheld is the offer, not the position - so this pins the offer rather than the URL.
    /// </summary>
    [Fact]
    public async Task An_unverified_account_is_not_offered_Google_Maps()
    {
        using var context = new MapContext();
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.ReadMyPositionCommand.ExecuteAsync(null);

        Assert.False(screen.CanOpenOwnPositionInGoogleMaps);
        Assert.NotNull(screen.OwnPositionInGoogleMapsUrl);
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
        private readonly GoogleIntegrationAccess _google;
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
                encryptionKeyProvider, new SyncGate(), NullLogger<EncryptedChatMessageSender>.Instance);
            _synchronizer = new ChatSynchronizer(
                _repository, chatClient, _usersClient, sender, NullLogger<ChatSynchronizer>.Instance);
            LocationClient = new LocationClient(LocationServer.ToHttpClient());
            _google = new GoogleIntegrationAccess(
                new AccountClient(_users.ToHttpClient(), FixedNetworkStatus.Online, sessionStore));
            _sharedLocations = new SharedLocations(
                LocationClient, _usersClient, encryptionKeyProvider, NullLogger<SharedLocations>.Instance);
        }

        /// <summary>Held so a test can say whether this account qualifies for the Google extras.</summary>
        public FakeUsersServer Users => _users;

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

        public MapViewModel Open(AppLanguage language = AppLanguage.English)
        {
            var translations = new Translations(new InMemoryLanguageStore());
            translations.SetLanguage(language);
            return new(Device, LocationClient, _sharedLocations, _usersClient, _repository, _synchronizer,
                translations, UnlockedPermissions.For(_localStore), _google, Navigator);
        }

        public void Dispose()
        {
            _chatServer.Dispose();
            LocationServer.Dispose();
            _users.Dispose();
            _localStore.Dispose();
        }
    }
}
