using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Data;

/// <summary>
/// A note's pictures on the handset: fetched once, read from the directory after that, and kept there as
/// the server holds them - a private note's picture stays sealed on disk and is opened only into memory.
/// </summary>
public sealed class NotePictureCacheTests : IDisposable
{
    private static readonly Guid NoteId = Guid.Parse("22222222-0000-4000-8000-000000000002");
    private static readonly Guid PictureId = Guid.Parse("33333333-0000-4000-8000-000000000003");
    private static readonly byte[] Picture = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"orbit-pictures-{Guid.NewGuid():N}");
    private readonly FakeNotesServer _server = new(TimeProvider.System);

    [Fact]
    public async Task A_picture_is_fetched_once_and_read_from_the_handset_after_that()
    {
        _server.Pictures[PictureId] = Picture;
        var cache = new NotePictureCache(_directory, new NotePicturesClient(_server.ToHttpClient()), PrivateContent.WithoutAKey());

        var first = await cache.OpenAsync(NoteId, PictureId, isSealed: false);
        _server.IsUnreachable = true;
        var second = await cache.OpenAsync(NoteId, PictureId, isSealed: false);

        Assert.Equal(Picture, first);
        Assert.Equal(Picture, second);
        Assert.True(cache.Holds(PictureId));
        Assert.Single(_server.ReceivedRequests, request => request.Contains("/pictures/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Nothing_on_the_handset_and_no_connection_is_no_picture_rather_than_an_error()
    {
        _server.IsUnreachable = true;
        var cache = new NotePictureCache(_directory, new NotePicturesClient(_server.ToHttpClient()), PrivateContent.WithoutAKey());

        Assert.Null(await cache.OpenAsync(NoteId, PictureId, isSealed: false));
        Assert.False(cache.Holds(PictureId));
    }

    [Fact]
    public async Task A_picture_the_server_no_longer_has_is_no_picture_and_is_not_kept()
    {
        var cache = new NotePictureCache(_directory, new NotePicturesClient(_server.ToHttpClient()), PrivateContent.WithoutAKey());

        Assert.Null(await cache.OpenAsync(NoteId, PictureId, isSealed: false));
        Assert.False(cache.Holds(PictureId));
    }

    /// <summary>The handset keeps what the server sent - the sealed bytes - and opens them into memory with the account's key.</summary>
    [Fact]
    public async Task A_sealed_picture_stays_sealed_on_the_handset_and_is_opened_with_the_key()
    {
        var userId = Guid.NewGuid();
        using var identity = ChatIdentity.Create();
        var sealedBytes = identity.SealBytesForSelf(Picture);
        _server.Pictures[PictureId] = sealedBytes;
        var cache = new NotePictureCache(_directory, new NotePicturesClient(_server.ToHttpClient()), await HoldingAsync(userId, identity));

        var opened = await cache.OpenAsync(NoteId, PictureId, isSealed: true);

        Assert.Equal(Picture, opened);
        Assert.Equal(sealedBytes, await File.ReadAllBytesAsync(Path.Combine(_directory, PictureId.ToString("N"))));
    }

    /// <summary>Fetched and kept for the day the key arrives, but not shown - there is nothing readable to show.</summary>
    [Fact]
    public async Task A_sealed_picture_without_the_key_is_kept_but_not_shown()
    {
        using var identity = ChatIdentity.Create();
        _server.Pictures[PictureId] = identity.SealBytesForSelf(Picture);
        var cache = new NotePictureCache(_directory, new NotePicturesClient(_server.ToHttpClient()), PrivateContent.WithoutAKey());

        Assert.Null(await cache.OpenAsync(NoteId, PictureId, isSealed: true));
        Assert.True(cache.Holds(PictureId));
    }

    [Fact]
    public async Task A_picture_through_a_link_is_fetched_by_the_token()
    {
        var share = new FakePublicShareServer();
        share.Pictures[PictureId] = Picture;
        var cache = new NotePictureCache(_directory, new NotePicturesClient(share.ToHttpClient()), PrivateContent.WithoutAKey());

        Assert.Equal(Picture, await cache.OpenSharedAsync("a-link-token", PictureId));
        Assert.True(cache.Holds(PictureId));
    }

    private static async Task<PrivateContentSealer> HoldingAsync(Guid userId, ChatIdentity identity)
    {
        var storage = new InMemoryChatKeyStorage();
        await storage.WritePrivateKeyJwkAsync(userId, identity.ExportPrivateKeyJwk());
        var session = new SessionStore(new InMemorySessionStorage(new UserSession("access", "refresh", userId, "user@orbit.example", "A User")));
        return new PrivateContentSealer(storage, session);
    }

    public void Dispose()
    {
        _server.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
