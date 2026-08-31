using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Notes;
using Orbit.Mobile.Data;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Data;

/// <summary>
/// What a migration leaves in a column it has just added. EF backfills every existing row with the
/// column's default, and the default it picks for TEXT is an empty string - which is not JSON, and threw
/// on the first read of every row that existed before the migration ran.
///
/// Nothing in the tests caught it, because a test builds its rows after the schema. It took a phone with
/// a warehouse and two task lists already on it, and it took the whole screen down.
/// </summary>
public sealed class BlankSnapshotColumnTests
{
    [Fact]
    public async Task A_row_whose_snapshot_column_is_blank_still_reads()
    {
        using var localStore = new LocalStore();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-31T10:00:00Z"));
        var notes = new LocalNoteRepository(localStore, clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());
        var note = await notes.CreateAsync("Shopping", [new NoteContentLineDto("milk", false, false)]);

        BlankTheSnapshot(localStore, note.LocalId);

        var stored = Assert.Single(await notes.GetAllAsync());
        Assert.Equal("Shopping", stored.Title);
        Assert.Empty(stored.CopyBaseLines);
    }

    /// <summary>Writes the column as a migration's backfill leaves it, going round the converter.</summary>
    private static void BlankTheSnapshot(LocalStore localStore, Guid localId)
    {
        using var dbContext = localStore.CreateDbContext();
        dbContext.Database.ExecuteSqlRaw(
            "UPDATE \"Notes\" SET \"CopyBaseLines\" = '' WHERE \"LocalId\" = {0}", localId);
    }
}
