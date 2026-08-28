using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Notes;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;
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

        Assert.Equal(
            LocalWriteOutcome.NotFound,
            await context.Repository.UpdateAsync(Guid.NewGuid(), "Ghost", SomeContent));
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

    [Fact]
    public async Task Pinning_a_note_queues_a_pin_rather_than_an_edit()
    {
        using var context = new RepositoryContext();
        var note = await context.Repository.CreateAsync("Groceries", SomeContent);

        await context.Repository.SetPinnedAsync(note.LocalId, true);

        var queued = await context.DbContext.Outbox.OrderBy(entry => entry.Id).ToListAsync();
        Assert.Equal([OutboxOperation.Create, OutboxOperation.SetPinned], queued.Select(entry => entry.Operation));
        Assert.True(await context.DbContext.Notes.Select(stored => stored.IsPinned).SingleAsync());
    }

    [Fact]
    public async Task Pinning_a_note_that_is_already_pinned_queues_nothing()
    {
        using var context = new RepositoryContext();
        var note = await context.Repository.CreateAsync("Groceries", SomeContent);
        await context.Repository.SetPinnedAsync(note.LocalId, true);

        await context.Repository.SetPinnedAsync(note.LocalId, true);

        Assert.Equal(2, await context.DbContext.Outbox.CountAsync());
    }

    [Fact]
    public async Task A_note_somebody_shared_cannot_be_pinned_here()
    {
        using var context = new RepositoryContext();
        var note = await context.Repository.CreateAsync("Theirs", SomeContent);
        await context.MarkAsSharedWithThisUserAsync(note.LocalId);

        var outcome = await context.Repository.SetPinnedAsync(note.LocalId, true);

        Assert.Equal(LocalWriteOutcome.NotYours, outcome);
        Assert.False(await context.DbContext.Notes.Select(stored => stored.IsPinned).SingleAsync());
    }

    /// <summary>
    /// The offline policy exists because two people can be editing one row. Nobody but the owner can
    /// pin, so it has nothing to say here - and a pin refused for being offline would be a restriction
    /// with no reason behind it.
    /// </summary>
    [Fact]
    public async Task A_note_shared_with_others_can_be_pinned_offline_even_though_it_cannot_be_edited()
    {
        using var context = new RepositoryContext(FixedNetworkStatus.Offline);
        var note = await context.Repository.CreateAsync("Ours", SomeContent);
        await context.MarkAsSharedWithOthersAsync(note.LocalId);

        Assert.Equal(
            LocalWriteOutcome.RefusedWhileOffline,
            await context.Repository.UpdateAsync(note.LocalId, "Renamed", SomeContent));
        Assert.Equal(LocalWriteOutcome.Applied, await context.Repository.SetPinnedAsync(note.LocalId, true));
    }

    [Fact]
    public async Task Pinned_notes_are_listed_before_the_rest()
    {
        using var context = new RepositoryContext();
        var older = await context.Repository.CreateAsync("Older", SomeContent);
        context.Clock.Advance(TimeSpan.FromMinutes(1));
        await context.Repository.CreateAsync("Newer", SomeContent);

        await context.Repository.SetPinnedAsync(older.LocalId, true);

        var notes = await context.Repository.GetAllAsync();
        Assert.Equal(["Older", "Newer"], notes.Select(note => note.Title));
    }

    private sealed class RepositoryContext : IDisposable
    {
        private readonly LocalStore _localStore = new();

        public RepositoryContext(INetworkStatus? networkStatus = null)
            => Repository = new LocalNoteRepository(_localStore, Clock, networkStatus ?? FixedNetworkStatus.Online);

        public FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
        public LocalNoteRepository Repository { get; }
        public OrbitLocalDbContext DbContext => _localStore.CreateDbContext();

        /// <summary>The same database as a later launch of the app would find it.</summary>
        public OrbitLocalDbContext Reopen() => _localStore.CreateDbContext();

        /// <summary>Leaves the note as a pull would leave one that arrived through somebody's share.</summary>
        public Task MarkAsSharedWithThisUserAsync(Guid localId)
            => SetSharingAsync(localId, isShared: true, isSharedWithOthers: false);

        /// <summary>Leaves the note as a pull would leave one this user owns and has shared out.</summary>
        public Task MarkAsSharedWithOthersAsync(Guid localId)
            => SetSharingAsync(localId, isShared: false, isSharedWithOthers: true);

        private async Task SetSharingAsync(Guid localId, bool isShared, bool isSharedWithOthers)
        {
            await using var dbContext = _localStore.CreateDbContext();
            var note = await dbContext.Notes.SingleAsync(candidate => candidate.LocalId == localId);
            note.IsShared = isShared;
            note.IsSharedWithOthers = isSharedWithOthers;
            await dbContext.SaveChangesAsync();
        }

        public void Dispose() => _localStore.Dispose();
    }
}
