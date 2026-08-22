using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Notes;
using Orbit.Core.Notes.DeleteNote;
using Xunit;

namespace Orbit.Api.Tests.Notes;

public sealed class DeleteNoteCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_deletes_a_note_owned_by_the_requesting_user()
    {
        var repository = new InMemoryNoteRepository();
        var handler = new DeleteNoteCommandHandler(repository);
        var userId = Guid.NewGuid();
        var note = Note.Create(userId, "Title", [NoteContentLine.PlainText("Content")]);
        await repository.AddAsync(note, CancellationToken.None);

        var wasDeleted = await handler.HandleAsync(new DeleteNoteCommand(userId, note.Id), CancellationToken.None);

        Assert.True(wasDeleted);
        Assert.Null(await repository.GetByIdAsync(userId, note.Id, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_returns_false_and_does_not_delete_a_note_owned_by_a_different_user()
    {
        var repository = new InMemoryNoteRepository();
        var handler = new DeleteNoteCommandHandler(repository);
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Title", [NoteContentLine.PlainText("Content")]);
        await repository.AddAsync(note, CancellationToken.None);

        var wasDeleted = await handler.HandleAsync(new DeleteNoteCommand(otherUserId, note.Id), CancellationToken.None);

        Assert.False(wasDeleted);
        Assert.NotNull(await repository.GetByIdAsync(ownerId, note.Id, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_returns_false_for_an_unknown_note_id()
    {
        var handler = new DeleteNoteCommandHandler(new InMemoryNoteRepository());

        var wasDeleted = await handler.HandleAsync(new DeleteNoteCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(wasDeleted);
    }
}
