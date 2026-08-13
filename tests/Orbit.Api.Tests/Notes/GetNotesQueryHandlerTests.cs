using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Notes;
using Orbit.Core.Notes.GetNotes;
using Xunit;

namespace Orbit.Api.Tests.Notes;

public sealed class GetNotesQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_only_notes_owned_by_the_requesting_user()
    {
        var repository = new InMemoryNoteRepository();
        var handler = new GetNotesQueryHandler(repository);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await repository.AddAsync(Note.Create(userId, "Mine", "Content"), CancellationToken.None);
        await repository.AddAsync(Note.Create(otherUserId, "Not mine", "Content"), CancellationToken.None);

        var notes = await handler.HandleAsync(new GetNotesQuery(userId), CancellationToken.None);

        var note = Assert.Single(notes);
        Assert.Equal("Mine", note.Title);
    }

    [Fact]
    public async Task HandleAsync_returns_an_empty_list_when_the_user_has_no_notes()
    {
        var handler = new GetNotesQueryHandler(new InMemoryNoteRepository());

        var notes = await handler.HandleAsync(new GetNotesQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(notes);
    }
}
