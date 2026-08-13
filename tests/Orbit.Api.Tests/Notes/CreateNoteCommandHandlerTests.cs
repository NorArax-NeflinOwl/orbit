using Orbit.Api.Tests.TestDoubles;
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

        var noteId = await handler.HandleAsync(new CreateNoteCommand(userId, "Title", "Content"), CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, noteId, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("Title", stored!.Title);
        Assert.Equal("Content", stored.Content);
    }
}
