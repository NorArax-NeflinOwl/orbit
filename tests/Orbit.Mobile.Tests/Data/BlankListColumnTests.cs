using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Tasks;
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
/// an inventory and two task lists already on it, and it took the whole screen down.
///
/// Checked for two of the columns rather than one: the reader is shared by all of them, and the point is
/// that the whole class of column survives it rather than only the one that failed.
/// </summary>
public sealed class BlankListColumnTests
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

    [Fact]
    public async Task A_task_lists_blank_item_column_reads_as_an_empty_list()
    {
        using var localStore = new LocalStore();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-31T10:00:00Z"));
        var taskLists = new LocalTaskListRepository(
            localStore, clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());
        var taskList = await taskLists.CreateAsync(
            "Errands", [new TaskItemDto(Guid.NewGuid(), "post office", null, false, null, "None", false, "None", new TimeOnly(9, 0))]);

        Blank(localStore, "TaskLists", "Items", taskList.LocalId);

        var stored = Assert.Single(await taskLists.GetAllAsync());
        Assert.Equal("Errands", stored.Title);
        Assert.Empty(stored.Items);
    }

    /// <summary>
    /// The same for the newest of these columns, which is a dictionary rather than a list: every shelf
    /// on every phone that upgrades has it backfilled blank, and reading one must answer "nothing known"
    /// rather than throwing.
    /// </summary>
    [Fact]
    public async Task A_shelfs_blank_arrivals_column_reads_as_nothing_known()
    {
        using var localStore = new LocalStore();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-31T10:00:00Z"));
        var inventories = new LocalInventoryRepository(
            localStore, clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());
        var inventory = await inventories.CreateAsync("Kitchen");

        Blank(localStore, "Inventories", "ItemArrivals", inventory.LocalId);

        var stored = Assert.Single(await inventories.GetAllAsync());
        Assert.Equal("Kitchen", stored.Name);
        Assert.Empty(stored.ItemArrivals);
    }

    /// <summary>Writes a column as a migration's backfill leaves it, going round the converter.</summary>
    private static void BlankTheSnapshot(LocalStore localStore, Guid localId)
        => Blank(localStore, "Notes", "CopyBaseLines", localId);

    private static void Blank(LocalStore localStore, string table, string column, Guid localId)
    {
        using var dbContext = localStore.CreateDbContext();

        // EF1002 warns that an interpolated string reaches the SQL unprotected, and it is right about the
        // shape. What it cannot see is that only the *identifiers* are interpolated here - and a table or
        // column name cannot be a parameter in any SQL dialect, so there is no version of this that
        // parameterises them. Both come from the two call sites above as literals; the one value that
        // varies, the id, goes through {0} as a real parameter.
#pragma warning disable EF1002
        dbContext.Database.ExecuteSqlRaw(
            $"UPDATE \"{table}\" SET \"{column}\" = '' WHERE \"LocalId\" = {{0}}", localId);
#pragma warning restore EF1002
    }
}
