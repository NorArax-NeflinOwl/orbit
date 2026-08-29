using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Core.Notes.CreateNote;
using Xunit;

namespace Orbit.Api.Tests.Notes;

public sealed class CreateNoteCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_creates_a_note_owned_by_the_requesting_user()
    {
        var repository = new InMemoryNoteRepository();
        var handler = new CreateNoteCommandHandler(repository);
        var userId = Guid.NewGuid();

        var noteId = await handler.HandleAsync(new CreateNoteCommand(userId, "Title", [NoteContentLine.PlainText("Content")], IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, noteId, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("Title", stored!.Title);
        Assert.Equal("Content", Assert.Single(stored.Content).Text);
    }

    [Fact]
    public async Task HandleAsync_keeps_the_priority_it_was_given()
    {
        var repository = new InMemoryNoteRepository();
        var userId = Guid.NewGuid();

        var noteId = await new CreateNoteCommandHandler(repository).HandleAsync(
            new CreateNoteCommand(
                userId, "Title", [NoteContentLine.PlainText("Content")], IsPrivate: false, EncryptedContent: null,
                ItemPriority.High),
            CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, noteId, CancellationToken.None);
        Assert.Equal(ItemPriority.High, stored!.Priority);
    }

    [Fact]
    public async Task HandleAsync_makes_an_ordinary_note_when_nothing_is_said_about_priority()
    {
        var repository = new InMemoryNoteRepository();
        var userId = Guid.NewGuid();

        var noteId = await new CreateNoteCommandHandler(repository).HandleAsync(
            new CreateNoteCommand(userId, "Title", [NoteContentLine.PlainText("Content")], IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, noteId, CancellationToken.None);
        Assert.Equal(ItemPriority.Normal, stored!.Priority);
    }
}
