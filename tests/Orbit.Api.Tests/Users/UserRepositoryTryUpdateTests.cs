using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Users;
using Orbit.Data.Repositories;
using Xunit;

namespace Orbit.Api.Tests.Users;

/// <summary>
/// A presence heartbeat reads the account, marks it seen and writes it back. When the account is deleted
/// between the two, the write used to fail with a concurrency exception that reached the caller as a 500 -
/// against the real repository only, which is why this runs against SQLite rather than the in-memory double.
/// </summary>
public sealed class UserRepositoryTryUpdateTests : IDisposable
{
    private readonly TemporarySqliteDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task An_account_deleted_since_it_was_read_is_not_written_and_does_not_throw()
    {
        var repository = new UserRepository(_database.DbContext);
        var read = await AnAccountReadBackAsync(repository);
        await new AccountDeletionRepository(_database.DbContext).DeleteAllDataForUserAsync(read.Id, CancellationToken.None);

        read.RecordSeen(DateTimeOffset.UtcNow);

        Assert.False(await repository.TryUpdateAsync(read, CancellationToken.None));
        Assert.Null(await repository.GetByIdAsync(read.Id, CancellationToken.None));
    }

    [Fact]
    public async Task An_account_still_there_is_written()
    {
        var repository = new UserRepository(_database.DbContext);
        var read = await AnAccountReadBackAsync(repository);
        var seenAtUtc = new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

        read.RecordSeen(seenAtUtc);

        Assert.True(await repository.TryUpdateAsync(read, CancellationToken.None));
        _database.DbContext.ChangeTracker.Clear();
        var stored = await repository.GetByIdAsync(read.Id, CancellationToken.None);
        Assert.Equal(read.Presence.StatusAt(seenAtUtc), stored!.Presence.StatusAt(seenAtUtc));
    }

    /// <summary>
    /// An account as a request finds it: added in one request, read back untracked in the next - which is
    /// why the tracker is cleared in between, the way a request's own scope would leave it.
    /// </summary>
    private async Task<User> AnAccountReadBackAsync(UserRepository repository)
    {
        var user = User.Create($"{Guid.NewGuid():N}@example.com", $"user{Guid.NewGuid():N}"[..20], "Somebody", "hash");
        await repository.AddAsync(user, CancellationToken.None);
        _database.DbContext.ChangeTracker.Clear();
        return (await repository.GetByIdAsync(user.Id, CancellationToken.None))!;
    }
}
