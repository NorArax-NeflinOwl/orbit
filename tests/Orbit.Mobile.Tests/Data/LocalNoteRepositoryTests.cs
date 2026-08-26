using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Notes;
using Orbit.Mobile.Data;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Data;

/// <summary>
/// Every local write has to record what it did as well as doing it. A change applied to the row but not
/// queued looks correct on screen and vanishes at the next pull, with nothing anywhere saying it ever
/// happened - so these check the pairing rather than either half alone.
/// </summary>
public sealed class LocalNoteRepositoryTests
{
    private static readonly IReadOnlyList<NoteContentLineDto> SomeContent =
        [new NoteContentLineDto("Milk", false, false)];

    [Fact]
    public async Task Creating_a_note_queues_exactly_one_change()
    {
        using var context = new RepositoryContext();

        await context.Repository.CreateAsync("Groceries", SomeContent);

        var queued = Assert.Single(await context.DbContext.Outbox.ToListAsync());
        Assert.Equal(OutboxOperation.Create, queued.Operation);
    }

    [Fact]
    public async Task A_note_created_offline_has_no_server_id_yet()
    {
        using var context = new RepositoryContext();

        var note = await context.Repository.CreateAsync("Groceries", SomeContent);

        Assert.Null(note.ServerId);
        Assert.Null(note.LastSyncedAtUtc);
    }

    [Fact]
    public async Task Editing_a_note_queues_the_edit_and_moves_its_timestamp()
    {
        using var context = new RepositoryContext();
        var note = await context.Repository.CreateAsync("Groceries", SomeContent);
        context.Clock.Advance(TimeSpan.FromMinutes(5));

        await context.Repository.UpdateAsync(note.LocalId, "Groceries and bread", SomeContent);

        var stored = await context.DbContext.Notes.SingleAsync();
        Assert.Equal("Groceries and bread", stored.Title);
        Assert.Equal(context.Clock.GetUtcNow(), stored.UpdatedAtUtc);
        Assert.Equal(2, await context.DbContext.Outbox.CountAsync());
    }

    [Fact]
    public async Task Editing_a_note_that_is_no_longer_there_reports_it_rather_than_failing()
    {
        using var context = new RepositoryContext();

        Assert.False(await context.Repository.UpdateAsync(Guid.NewGuid(), "Ghost", SomeContent));
    }

    [Fact]
    public async Task A_notes_lines_survive_a_round_trip_through_the_database()
    {
        using var context = new RepositoryContext();
        IReadOnlyList<NoteContentLineDto> lines =
            [new NoteContentLineDto("Milk", true, true), new NoteContentLineDto("Bread", true, false)];

        var note = await context.Repository.CreateAsync("Groceries", lines);

        // Lines are stored as JSON in one column, so getting them back intact is not free.
        var reopened = await context.Reopen().Notes.SingleAsync(stored => stored.LocalId == note.LocalId);
        Assert.Equal(lines, reopened.Content);
    }

    [Fact]
    public async Task Editing_the_lines_of_a_note_is_actually_saved()
    {
        using var context = new RepositoryContext();
        var note = await context.Repository.CreateAsync("Groceries", SomeContent);

        await context.Repository.UpdateAsync(note.LocalId, "Groceries", [new NoteContentLineDto("Bread", false, false)]);

        // Without a value comparer EF compares the converted JSON by reference and saves nothing.
        var reopened = await context.Reopen().Notes.SingleAsync(stored => stored.LocalId == note.LocalId);
        Assert.Equal("Bread", Assert.Single(reopened.Content).Text);
    }

    [Fact]
    public async Task A_note_with_unsent_changes_can_be_told_apart_from_one_without()
    {
        using var context = new RepositoryContext();
        var pending = await context.Repository.CreateAsync("Written on a train", SomeContent);

        var withPendingChanges = await context.Repository.GetPendingNoteLocalIdsAsync();

        Assert.Equal([pending.LocalId], withPendingChanges);
    }

    [Fact]
    public async Task Notes_are_listed_most_recently_changed_first()
    {
        using var context = new RepositoryContext();
        await context.Repository.CreateAsync("Older", SomeContent);
        context.Clock.Advance(TimeSpan.FromMinutes(1));
        await context.Repository.CreateAsync("Newer", SomeContent);

        var notes = await context.Repository.GetAllAsync();

        Assert.Equal(["Newer", "Older"], notes.Select(note => note.Title));
    }

    private sealed class RepositoryContext : IDisposable
    {
        private readonly LocalStore _localStore = new();

        public RepositoryContext() => Repository = new LocalNoteRepository(_localStore, Clock);

        public FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
        public LocalNoteRepository Repository { get; }
        public OrbitLocalDbContext DbContext => _localStore.CreateDbContext();

        /// <summary>The same database as a later launch of the app would find it.</summary>
        public OrbitLocalDbContext Reopen() => _localStore.CreateDbContext();

        public void Dispose() => _localStore.Dispose();
    }
}
