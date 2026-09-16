using Orbit.Api.Auth;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Notes;
using Orbit.Core.Users;
using Orbit.Core.Users.DeleteAccount;
using Xunit;

namespace Orbit.Api.Tests.Users;

/// <summary>
/// A note's pictures are bytes outside the database, found by the id of the row that names them - so an
/// account deleted without sweeping them first leaves them in the store for as long as the store lives,
/// with nothing left anywhere that could ever name them again. Sealed ones included, which is the half
/// that matters: they are the reader's own ciphertext, kept after the account that could open it is gone.
///
/// The rows are the deletion repository's (and covered by AccountDeletionSweepTests); the bytes are the
/// handler's, because only it has the store. Both halves went in on 2026-09-15, after compiling the
/// branch made the sweep test name NotePictureEntity as a table nothing deleted.
/// </summary>
public sealed class DeletedAccountLeavesNoPicturesTests
{
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task Deleting_the_account_takes_its_pictures_bytes_with_it()
    {
        var context = new DeletionContext();
        var mine = await context.AddPictureAsync(_userId);

        var deleted = await context.Handler().HandleAsync(
            new DeleteAccountCommand(_userId, "hunter2"), CancellationToken.None);

        Assert.True(deleted);
        Assert.DoesNotContain(mine, context.Store.Bytes.Keys);
    }

    [Fact]
    public async Task Somebody_elses_pictures_are_left_where_they_are()
    {
        var context = new DeletionContext();
        await context.AddPictureAsync(_userId);
        var theirs = await context.AddPictureAsync(Guid.NewGuid());

        await context.Handler().HandleAsync(new DeleteAccountCommand(_userId, "hunter2"), CancellationToken.None);

        Assert.Contains(theirs, context.Store.Bytes.Keys);
    }

    /// <summary>
    /// And nothing is swept for an account that could not prove it is the owner - the account is still
    /// there afterwards, so taking its pictures away would be destroying what it still has.
    /// </summary>
    [Fact]
    public async Task A_refused_deletion_takes_nothing()
    {
        var context = new DeletionContext();
        var mine = await context.AddPictureAsync(_userId);

        var deleted = await context.Handler().HandleAsync(
            new DeleteAccountCommand(_userId, "the wrong one"), CancellationToken.None);

        Assert.False(deleted);
        Assert.Contains(mine, context.Store.Bytes.Keys);
    }

    private sealed class DeletionContext
    {
        private readonly PasswordHasher _hasher = new();
        private readonly InMemoryUserRepository _users = new();
        private readonly InMemoryNotePictureRepository _pictures = new();

        public InMemoryNotePictureStore Store { get; } = new();

        public DeleteAccountCommandHandler Handler()
            => new(
                _users, _hasher, new InMemoryAccountDeletionRepository(), new InMemoryChatGroupRepository(),
                // Never consulted: nothing here sends a token, so the password is what has to prove it.
                new StubGoogleIdentityVerifier(), _pictures, Store);

        /// <summary>A picture of that account's, bytes and row, as an upload leaves them.</summary>
        public async Task<Guid> AddPictureAsync(Guid ownerUserId)
        {
            await EnsureUserAsync(ownerUserId);
            var pictureId = Guid.NewGuid();
            using var bytes = new MemoryStream([1, 2, 3]);
            var size = await Store.WriteAsync(pictureId, bytes, CancellationToken.None);
            await _pictures.AddAsync(
                NotePicture.Create(pictureId, Guid.NewGuid(), ownerUserId, size, "image/png", isSealed: false),
                CancellationToken.None);
            return pictureId;
        }

        private async Task EnsureUserAsync(Guid userId)
        {
            if (await _users.GetByIdAsync(userId, CancellationToken.None) is not null)
            {
                return;
            }

            await _users.AddAsync(
                User.FromPersistence(
                    userId, $"{userId:N}@orbit.example", $"user{userId:N}", "Somebody",
                    _hasher.Hash("hunter2"), DateTimeOffset.UtcNow, publicKeyBase64: null),
                CancellationToken.None);
        }
    }
}
