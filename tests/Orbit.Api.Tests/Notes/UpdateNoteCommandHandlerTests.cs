using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Core.Notes.UpdateNote;
using Xunit;

namespace Orbit.Api.Tests.Notes;

public sealed class UpdateNoteCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_updates_a_note_owned_by_the_requesting_user()
    {
        var repository = new InMemoryNoteRepository();
        var handler = new UpdateNoteCommandHandler(repository);
        var userId = Guid.NewGuid();
        var note = Note.Create(userId, "Original title", [NoteContentLine.PlainText("Original content")]);
        await repository.AddAsync(note, CancellationToken.None);

        var wasUpdated = await handler.HandleAsync(
            new UpdateNoteCommand(userId, note.Id, "New title", [NoteContentLine.PlainText("New content")]), CancellationToken.None);

        Assert.True(wasUpdated);
        var stored = await repository.GetByIdAsync(userId, note.Id, CancellationToken.None);
        Assert.Equal("New title", stored!.Title);
        Assert.Equal("New content", Assert.Single(stored.Content).Text);
    }

    [Fact]
    public async Task HandleAsync_returns_false_and_does_not_update_a_note_owned_by_a_different_user()
    {
        var repository = new InMemoryNoteRepository();
        var handler = new UpdateNoteCommandHandler(repository);
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Original title", [NoteContentLine.PlainText("Original content")]);
        await repository.AddAsync(note, CancellationToken.None);

        var wasUpdated = await handler.HandleAsync(
            new UpdateNoteCommand(otherUserId, note.Id, "Hijacked title", [NoteContentLine.PlainText("Hijacked content")]), CancellationToken.None);

        Assert.False(wasUpdated);
        var stored = await repository.GetByIdAsync(ownerId, note.Id, CancellationToken.None);
        Assert.Equal("Original title", stored!.Title);
    }

    [Fact]
    public async Task HandleAsync_returns_false_and_does_not_update_a_shared_read_only_note()
    {
        var repository = new InMemoryNoteRepository();
        var handler = new UpdateNoteCommandHandler(repository);
        var recipientId = Guid.NewGuid();
        var sharedNote = Note.CreateShared(recipientId, "Original title", [NoteContentLine.PlainText("Original content")], "owner", ShareAccessLevel.ReadOnly);
        await repository.AddAsync(sharedNote, CancellationToken.None);

        var wasUpdated = await handler.HandleAsync(
            new UpdateNoteCommand(recipientId, sharedNote.Id, "Edited title", [NoteContentLine.PlainText("Edited content")]), CancellationToken.None);

        Assert.False(wasUpdated);
        var stored = await repository.GetByIdAsync(recipientId, sharedNote.Id, CancellationToken.None);
        Assert.Equal("Original title", stored!.Title);
    }

    [Fact]
    public async Task HandleAsync_updates_a_shared_note_with_edit_access()
    {
        var repository = new InMemoryNoteRepository();
        var handler = new UpdateNoteCommandHandler(repository);
        var recipientId = Guid.NewGuid();
        var sharedNote = Note.CreateShared(recipientId, "Original title", [NoteContentLine.PlainText("Original content")], "owner", ShareAccessLevel.CanEdit);
        await repository.AddAsync(sharedNote, CancellationToken.None);

        var wasUpdated = await handler.HandleAsync(
            new UpdateNoteCommand(recipientId, sharedNote.Id, "New title", [NoteContentLine.PlainText("New content")]), CancellationToken.None);

        Assert.True(wasUpdated);
        var stored = await repository.GetByIdAsync(recipientId, sharedNote.Id, CancellationToken.None);
        Assert.Equal("New title", stored!.Title);
    }

    [Fact]
    public async Task HandleAsync_returns_false_for_an_unknown_note_id()
    {
        var handler = new UpdateNoteCommandHandler(new InMemoryNoteRepository());

        var wasUpdated = await handler.HandleAsync(
            new UpdateNoteCommand(Guid.NewGuid(), Guid.NewGuid(), "Title", [NoteContentLine.PlainText("Content")]), CancellationToken.None);

        Assert.False(wasUpdated);
    }
}
