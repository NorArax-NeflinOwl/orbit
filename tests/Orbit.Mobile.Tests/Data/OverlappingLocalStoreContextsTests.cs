using Microsoft.EntityFrameworkCore;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Data;

/// <summary>
/// What the local store's test double guarantees: a connection of its own for every context, onto one
/// database per store.
///
/// It used to hand every context the same connection, because a ":memory:" database belongs to whoever
/// opened it. That is not what the phone does - its store is a file, and each context opens its own -
/// and it is the only arrangement under which two contexts can collide on one connection. CI once threw
/// "unable to delete/modify user-function due to active statements" from EF initialising a connection,
/// which is what SQLite says when functions are registered while a statement on that connection is
/// still open.
///
/// Said plainly: that failure has not been reproduced, here or under forced parallelism, so these tests
/// do not stand guard over it and are not claimed to. What they pin is the arrangement itself - which
/// removes the only mechanism by which that error can arise, and which is easy to undo by accident
/// while "simplifying" the double back to a single shared connection.
/// </summary>
public sealed class OverlappingLocalStoreContextsTests
{
    /// <summary>Two contexts alive at once, one mid-read - what a screen reading during a save does.</summary>
    [Fact]
    public async Task A_second_context_opens_while_the_first_is_still_reading()
    {
        using var localStore = new LocalStore();

        await using var reading = localStore.CreateDbContext();
        // Streamed rather than listed, so the reader is genuinely still open when the second context is
        // created - ToListAsync would have closed it first.
        await using var rows = reading.TaskLists.AsAsyncEnumerable().GetAsyncEnumerator();
        await rows.MoveNextAsync();

        await using var writing = localStore.CreateDbContext();

        Assert.Empty(await writing.TaskLists.ToListAsync());
    }

    /// <summary>
    /// And they are looking at the same database, which is the other half of the arrangement: separate
    /// connections onto a shared-cache database, not separate databases.
    /// </summary>
    [Fact]
    public async Task What_one_context_writes_the_next_one_reads()
    {
        using var localStore = new LocalStore();

        await using (var writing = localStore.CreateDbContext())
        {
            writing.TaskLists.Add(new Orbit.Mobile.Data.LocalTaskList
            {
                LocalId = Guid.NewGuid(),
                Title = "Errands",
                Items = [],
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await writing.SaveChangesAsync();
        }

        await using var reading = localStore.CreateDbContext();

        Assert.Equal("Errands", Assert.Single(await reading.TaskLists.ToListAsync()).Title);
    }

    /// <summary>One store is one database: two of them must not see each other's rows.</summary>
    [Fact]
    public async Task Two_stores_are_two_databases()
    {
        using var one = new LocalStore();
        using var other = new LocalStore();

        await using (var writing = one.CreateDbContext())
        {
            writing.TaskLists.Add(new Orbit.Mobile.Data.LocalTaskList
            {
                LocalId = Guid.NewGuid(),
                Title = "Only here",
                Items = [],
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await writing.SaveChangesAsync();
        }

        await using var reading = other.CreateDbContext();

        Assert.Empty(await reading.TaskLists.ToListAsync());
    }
}
