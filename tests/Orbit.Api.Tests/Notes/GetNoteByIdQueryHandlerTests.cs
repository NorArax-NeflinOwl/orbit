using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Notes;
using Orbit.Core.Notes.GetNoteById;
using Xunit;

namespace Orbit.Api.Tests.Notes;

public sealed class GetNoteByIdQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_the_note_when_owned_by_the_requesting_user()
    {
        var repository = new InMemoryNoteRepository();
        var handler = new GetNoteByIdQueryHandler(repository);
        var userId = Guid.NewGuid();
        var note = Note.Create(userId, "Title", [NoteContentLine.PlainText("Content")]);
        await repository.AddAsync(note, CancellationToken.None);

        var result = await handler.HandleAsync(new GetNoteByIdQuery(userId, note.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(note.Id, result!.Id);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_a_note_owned_by_a_different_user()
    {
        var repository = new InMemoryNoteRepository();
        var handler = new GetNoteByIdQueryHandler(repository);
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Title", [NoteContentLine.PlainText("Content")]);
        await repository.AddAsync(note, CancellationToken.None);

        var result = await handler.HandleAsync(new GetNoteByIdQuery(otherUserId, note.Id), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_unknown_note_id()
    {
        var handler = new GetNoteByIdQueryHandler(new InMemoryNoteRepository());

        var result = await handler.HandleAsync(new GetNoteByIdQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }
}
