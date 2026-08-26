using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Location;
using Orbit.Mobile.Tests.Crypto;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Location;

/// <summary>
/// Sharing where you are. A position is sealed for one recipient with the pairwise key those two
/// already use for chat, so the server relays something it cannot open.
///
/// The sharpest risk here is not the crypto - that is the same code messages use - but the shape inside
/// the ciphertext. Orbit.Web serialises the position with the member names as they are spelled, and
/// nothing in the protocol would report a disagreement: the server holds ciphertext, so a mismatch
/// surfaces as a position that opens into nulls on somebody else's phone.
/// </summary>
public sealed class SharedLocationTests
{
    private static readonly SharedPosition Somewhere =
        new(52.2297, 21.0122, "Marszałkowska, Warszawa", DateTimeOffset.Parse("2026-08-26T10:00:00Z"));

    [Fact]
    public void The_sealed_payload_uses_the_names_the_web_client_writes()
    {
        // Orbit.Web calls JsonSerializer.Serialize(position) with no options, so these exact spellings
        // are the wire format. A naming policy slipped in here would break every cross-client share
        // without a single error anywhere.
        using var document = JsonDocument.Parse(Somewhere.ToJson());

        // The set, not the order - reordering the record is harmless, renaming a member is not.
        var names = document.RootElement.EnumerateObject().Select(property => property.Name).ToHashSet();
        Assert.Equal(["Latitude", "Longitude", "Address", "RecordedAtUtc"], names);
    }

    [Fact]
    public void A_position_survives_being_written_and_read_back()
    {
        var read = SharedPosition.FromJson(Somewhere.ToJson());

        Assert.Equal(Somewhere, read);
    }

    [Fact]
    public void Plaintext_that_is_not_a_position_comes_back_as_nothing()
    {
        Assert.Null(SharedPosition.FromJson("just a message"));
        Assert.Null(SharedPosition.FromJson("{ this never parses"));
    }

    [Fact]
    public async Task A_shared_position_is_sealed_so_only_the_recipient_can_open_it()
    {
        using var context = new LocationContext();
        context.GiveTheOtherPartyAPublishedKey();

        Assert.True(await context.SharedLocations.ShareAsync(context.OtherUserId, Somewhere, isContinuous: false));

        // The server stored ciphertext; only their key turns it back into a place.
        var share = Assert.Single(context.Server.Shares);
        var opened = SharedPosition.FromJson(context.OpenAsTheOtherParty(share)!);
        Assert.Equal(Somewhere, opened);
    }

    [Fact]
    public async Task Sharing_with_somebody_who_has_no_key_is_refused_rather_than_pretended()
    {
        // Sharing into nothing would look exactly like sharing.
        using var context = new LocationContext();

        Assert.False(await context.SharedLocations.ShareAsync(context.OtherUserId, Somewhere, isContinuous: false));
        Assert.Empty(context.Server.Shares);
    }

    [Fact]
    public async Task A_position_shared_with_the_reader_is_opened_and_named()
    {
        using var context = new LocationContext();
        context.GiveTheOtherPartyAPublishedKey();
        var sealedPosition = context.OtherIdentity.Encrypt(context.OwnPublicKeyBase64, Somewhere.ToJson());
        context.Server.AddIncomingShare(
            context.OtherUserId, sealedPosition.CiphertextBase64, sealedPosition.NonceBase64, isContinuous: true);

        var received = Assert.Single(await context.SharedLocations.ReadSharedWithMeAsync());

        Assert.Equal("Bob", received.SharerDisplayName);
        Assert.True(received.IsContinuous);
        Assert.Equal(Somewhere, received.Position);
    }

    [Fact]
    public async Task A_position_that_cannot_be_opened_is_still_listed()
    {
        // That somebody is sharing and this device cannot read it is worth seeing - quietly showing one
        // fewer person than are actually sharing would be worse.
        using var context = new LocationContext();
        context.GiveTheOtherPartyAPublishedKey();
        context.Server.AddIncomingShare(context.OtherUserId, "AAAAAAAAAAAAAAAAAAAAAA==", "AAAAAAAAAAAAAAAA");

        var received = Assert.Single(await context.SharedLocations.ReadSharedWithMeAsync());

        Assert.True(received.CannotBeOpened);
        Assert.Equal("Bob", received.SharerDisplayName);
    }

    [Fact]
    public async Task Sharing_again_replaces_what_that_person_could_see()
    {
        using var context = new LocationContext();
        context.GiveTheOtherPartyAPublishedKey();
        await context.SharedLocations.ShareAsync(context.OtherUserId, Somewhere, isContinuous: false);

        var later = Somewhere with { Latitude = 50.0647, Longitude = 19.9450, Address = "Kraków" };
        await context.SharedLocations.ShareAsync(context.OtherUserId, later, isContinuous: false);

        var share = Assert.Single(context.Server.Shares);
        Assert.Equal("Kraków", SharedPosition.FromJson(context.OpenAsTheOtherParty(share)!)!.Address);
    }

    private sealed class LocationContext : IDisposable
    {
        private readonly FakeUsersServer _users = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));

        public LocationContext()
        {
            Server = new FakeLocationServer(_clock);
            var vectors = BrowserVectorsFile.Read();

            OwnUserId = Guid.NewGuid();
            OtherUserId = Guid.NewGuid();
            Server.CallerUserId = OwnUserId;
            OtherIdentity = ChatIdentity.FromBackup(vectors.Bob.Backup, vectors.BackupPassword)!;
            OtherPublicKeyBase64 = vectors.Bob.PublicKeyBase64;

            var keyStorage = new InMemoryChatKeyStorage();
            using (var own = ChatIdentity.FromBackup(vectors.Alice.Backup, vectors.BackupPassword)!)
            {
                keyStorage.WritePrivateKeyJwkAsync(OwnUserId, own.ExportPrivateKeyJwk()).GetAwaiter().GetResult();
                OwnPublicKeyBase64 = own.PublicKeyBase64;
            }

            var sessionStore = new SessionStore(new InMemorySessionStorage(
                new UserSession("access", "refresh", OwnUserId, "me@orbit.example", "Me")));
            var encryptionKeyProvider = new OwnEncryptionKeyProvider(
                keyStorage, new EncryptionKeyClient(new FakeEncryptionKeyServer().ToHttpClient()),
                sessionStore, NullLogger<OwnEncryptionKeyProvider>.Instance);

            LocationClient = new LocationClient(Server.ToHttpClient());
            SharedLocations = new SharedLocations(
                LocationClient, new UsersClient(_users.ToHttpClient()), encryptionKeyProvider,
                NullLogger<SharedLocations>.Instance);
        }

        public FakeLocationServer Server { get; }
        public LocationClient LocationClient { get; }
        public SharedLocations SharedLocations { get; }
        public Guid OwnUserId { get; }
        public Guid OtherUserId { get; }
        public string OwnPublicKeyBase64 { get; }
        public string OtherPublicKeyBase64 { get; }
        public ChatIdentity OtherIdentity { get; }

        public void GiveTheOtherPartyAPublishedKey() => _users.Add(OtherUserId, "Bob", OtherPublicKeyBase64);

        public string? OpenAsTheOtherParty(Orbit.Contracts.Users.SharedLocationDto share)
            => OtherIdentity.Decrypt(OwnPublicKeyBase64, new EncryptedText(share.CiphertextBase64, share.NonceBase64));

        public void Dispose()
        {
            OtherIdentity.Dispose();
            Server.Dispose();
            _users.Dispose();
        }
    }
}
