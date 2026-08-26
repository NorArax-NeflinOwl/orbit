using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Notes;
using Orbit.Core.Notes.DeleteNote;
using Orbit.Core.Sync;
using Xunit;

namespace Orbit.Api.Tests.Sync;

/// <summary>
/// A deletion is the one change a delta cannot carry on its own: the row is simply absent, which reads
/// exactly like a row the client already has. These pin down that deleting a note leaves the trace an
/// offline client needs to find out, and that nothing is left behind when there was nothing to delete.
/// </summary>
public sealed class NoteDeletionTombstoneTests
{
    [Fact]
    public async Task Deleting_a_note_records_that_it_was_deleted()
    {
        var context = new NoteDeletionContext();
        var noteId = await context.AddNoteAsync();

        var deleted = await context.DeleteAsync(noteId);

        Assert.True(deleted);
        var tombstone = Assert.Single(context.Tombstones.Tombstones);
        Assert.Equal(context.UserId, tombstone.UserId);
        Assert.Equal(SyncEntityType.Note, tombstone.EntityType);
        Assert.Equal(noteId, tombstone.EntityId);
    }

    [Fact]
    public async Task A_note_that_was_never_there_leaves_no_trace()
    {
        // Nothing was deleted, so claiming otherwise would make clients drop a note they should keep.
        var context = new NoteDeletionContext();

        var deleted = await context.DeleteAsync(Guid.NewGuid());

        Assert.False(deleted);
        Assert.Empty(context.Tombstones.Tombstones);
    }

    [Fact]
    public async Task Another_users_note_leaves_no_trace_either()
    {
        var context = new NoteDeletionContext();
        var noteId = await context.AddNoteAsync(ownerUserId: Guid.NewGuid());

        var deleted = await context.DeleteAsync(noteId);

        Assert.False(deleted);
        Assert.Empty(context.Tombstones.Tombstones);
    }

    [Fact]
    public async Task A_deletion_is_reported_to_a_client_that_was_away_when_it_happened()
    {
        var context = new NoteDeletionContext();
        var noteId = await context.AddNoteAsync();
        var lastSyncedAt = DateTimeOffset.UtcNow.AddMinutes(-5);

        await context.DeleteAsync(noteId);

        var deletedIds = await context.Tombstones.GetDeletedIdsSinceAsync(
            context.UserId, SyncEntityType.Note, lastSyncedAt, CancellationToken.None);
        Assert.Equal([noteId], deletedIds);
    }

    [Fact]
    public async Task A_deletion_the_client_already_knows_about_is_not_reported_again()
    {
        var context = new NoteDeletionContext();
        var noteId = await context.AddNoteAsync();
        await context.DeleteAsync(noteId);

        // Catching up from a cursor taken after the deletion: it is already applied locally.
        var deletedIds = await context.Tombstones.GetDeletedIdsSinceAsync(
            context.UserId, SyncEntityType.Note, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None);

        Assert.Empty(deletedIds);
    }

    [Fact]
    public async Task One_users_deletions_are_invisible_to_another()
    {
        var context = new NoteDeletionContext();
        var noteId = await context.AddNoteAsync();
        await context.DeleteAsync(noteId);

        var deletedIds = await context.Tombstones.GetDeletedIdsSinceAsync(
            Guid.NewGuid(), SyncEntityType.Note, DateTimeOffset.UtcNow.AddMinutes(-5), CancellationToken.None);

        Assert.Empty(deletedIds);
    }

    [Fact]
    public async Task Deletions_of_another_kind_of_thing_are_not_mixed_in()
    {
        // One table serves every entity type, so the type filter is what keeps a deleted note from
        // telling a client to drop a task list with the same id.
        var context = new NoteDeletionContext();
        await context.DeleteAsync(await context.AddNoteAsync());

        var deletedIds = await context.Tombstones.GetDeletedIdsSinceAsync(
            context.UserId, SyncEntityType.TaskList, DateTimeOffset.UtcNow.AddMinutes(-5), CancellationToken.None);

        Assert.Empty(deletedIds);
    }

    private sealed class NoteDeletionContext
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public InMemoryNoteRepository Notes { get; } = new();
        public InMemorySyncTombstoneRepository Tombstones { get; } = new();

        public async Task<Guid> AddNoteAsync(Guid? ownerUserId = null)
        {
            var note = Note.Create(ownerUserId ?? UserId, "Note", [NoteContentLine.PlainText("Body")]);
            await Notes.AddAsync(note, CancellationToken.None);
            return note.Id;
        }

        public Task<bool> DeleteAsync(Guid noteId)
            => new DeleteNoteCommandHandler(Notes, Tombstones)
                .HandleAsync(new DeleteNoteCommand(UserId, noteId), CancellationToken.None);
    }
}
