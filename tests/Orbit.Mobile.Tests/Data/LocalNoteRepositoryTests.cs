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

        await context.Repository.UpdateAsync(note.LocalId, new NoteContent("Groceries and bread", SomeContent, "Normal"));

        var stored = await context.DbContext.Notes.SingleAsync();
        Assert.Equal("Groceries and bread", stored.Title);
        Assert.Equal(context.Clock.GetUtcNow(), stored.UpdatedAtUtc);
        Assert.Equal(2, await context.DbContext.Outbox.CountAsync());
    }

    [Fact]
    public async Task Editing_a_note_that_is_no_longer_there_reports_it_rather_than_failing()
    {
        using var context = new RepositoryContext();

        Assert.Equal(
            LocalWriteOutcome.NotFound,
            await context.Repository.UpdateAsync(Guid.NewGuid(), new NoteContent("Ghost", SomeContent, "Normal")));
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

        await context.Repository.UpdateAsync(
            note.LocalId, new NoteContent("Groceries", [new NoteContentLineDto("Bread", false, false)], "Normal"));

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

    /// <summary>
    /// The guard behind the screen. A read-only share opened as an editable screen once, and the edit
    /// was queued for a server that answers 403 - so the work was lost some minutes later with nothing
    /// saying why. Refused here as well as hidden there: a screen that forgets to ask must not be able
    /// to queue it.
    /// </summary>
    [Fact]
    public async Task Something_shared_to_read_cannot_be_edited_and_queues_nothing()
    {
        using var context = new RepositoryContext();
        var note = await context.SharedWithMeAsync("ReadOnly");

        var outcome = await context.Repository.UpdateAsync(
            note.LocalId, new NoteContent("Mine now", SomeContent, "Normal"));

        Assert.Equal(LocalWriteOutcome.RefusedAsReadOnly, outcome);
        Assert.Empty(await context.DbContext.Outbox.ToListAsync());
        Assert.False(await context.Repository.CanEditAsync(note.LocalId));
    }

    [Fact]
    public async Task Something_shared_for_editing_is_edited_as_usual()
    {
        using var context = new RepositoryContext();
        var note = await context.SharedWithMeAsync("EditOnly");

        var outcome = await context.Repository.UpdateAsync(
            note.LocalId, new NoteContent("Reworded", SomeContent, "Normal"));

        Assert.Equal(LocalWriteOutcome.Applied, outcome);
        Assert.True(await context.Repository.CanEditAsync(note.LocalId));
    }

    /// <summary>
    /// Deleting something shared with you is not editing it: the server takes it off your list and
    /// leaves the owner's alone (see DeleteNoteCommandHandler), so the phone must not refuse it.
    /// </summary>
    [Fact]
    public async Task Something_shared_to_read_can_still_be_taken_off_your_own_list()
    {
        using var context = new RepositoryContext();
        var note = await context.SharedWithMeAsync("ReadOnly");

        Assert.Equal(LocalWriteOutcome.Applied, await context.Repository.DeleteAsync(note.LocalId));
    }

    private sealed class RepositoryContext : IDisposable
    {
        private readonly LocalStore _localStore = new();

        public RepositoryContext() => Repository = new LocalNoteRepository(_localStore, Clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());

        public FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
        public LocalNoteRepository Repository { get; }
        public OrbitLocalDbContext DbContext => _localStore.CreateDbContext();

        /// <summary>The same database as a later launch of the app would find it.</summary>
        public OrbitLocalDbContext Reopen() => _localStore.CreateDbContext();

        /// <summary>
        /// A note that arrived through somebody else's share, as a pull would have written it - see
        /// NoteSynchronizer, which is what sets these three together.
        /// </summary>
        public async Task<LocalNote> SharedWithMeAsync(string accessLevel)
        {
            await using var dbContext = _localStore.CreateDbContext();
            var note = new LocalNote
            {
                LocalId = Guid.NewGuid(),
                ServerId = Guid.NewGuid(),
                Title = "Somebody else's note",
                IsShared = true,
                SharedByUserName = "ala",
                AccessLevel = accessLevel,
                CreatedAtUtc = Clock.GetUtcNow(),
                UpdatedAtUtc = Clock.GetUtcNow()
            };

            dbContext.Notes.Add(note);
            await dbContext.SaveChangesAsync();
            return note;
        }

        public void Dispose() => _localStore.Dispose();
    }
}
