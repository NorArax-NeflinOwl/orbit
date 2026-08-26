using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
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
        var handler = new DeleteNoteCommandHandler(repository, new InMemoryNoteShareRepository(), new InMemorySyncTombstoneRepository());
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
        var handler = new DeleteNoteCommandHandler(repository, new InMemoryNoteShareRepository(), new InMemorySyncTombstoneRepository());
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
        var handler = new DeleteNoteCommandHandler(new InMemoryNoteRepository(), new InMemoryNoteShareRepository(), new InMemorySyncTombstoneRepository());

        var wasDeleted = await handler.HandleAsync(new DeleteNoteCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(wasDeleted);
    }
    [Fact]
    public async Task A_note_somebody_shared_with_you_comes_off_your_list_without_touching_theirs()
    {
        var noteRepository = new InMemoryNoteRepository();
        var shareRepository = new InMemoryNoteShareRepository();
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Shopping", [NoteContentLine.PlainText("Milk")]);
        await noteRepository.AddAsync(note, CancellationToken.None);
        var share = NoteShare.Create(note.Id, ownerId, recipientId, ShareAccessLevel.ReadOnly);
        share.MarkAccepted();
        await shareRepository.AddAsync(share, CancellationToken.None);

        var handler = new DeleteNoteCommandHandler(noteRepository, shareRepository, new InMemorySyncTombstoneRepository());
        var removed = await handler.HandleAsync(new DeleteNoteCommand(recipientId, note.Id), CancellationToken.None);

        // The recipient pressed Delete and it worked - it used to 404, because the note is not theirs to
        // delete and nothing offered to drop the grant instead.
        Assert.True(removed);
        Assert.Null(await shareRepository.FindAcceptedGrantAsync(note.Id, recipientId, CancellationToken.None));
        // And the owner still has it.
        Assert.NotNull(await noteRepository.GetByIdAsync(ownerId, note.Id, CancellationToken.None));
    }

    [Fact]
    public async Task A_note_nobody_shared_with_you_is_still_a_404()
    {
        var noteRepository = new InMemoryNoteRepository();
        var ownerId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Shopping", [NoteContentLine.PlainText("Milk")]);
        await noteRepository.AddAsync(note, CancellationToken.None);

        var handler = new DeleteNoteCommandHandler(noteRepository, new InMemoryNoteShareRepository(), new InMemorySyncTombstoneRepository());
        var removed = await handler.HandleAsync(new DeleteNoteCommand(Guid.NewGuid(), note.Id), CancellationToken.None);

        // Dropping a grant that was never offered would be a way to probe which ids exist.
        Assert.False(removed);
        Assert.NotNull(await noteRepository.GetByIdAsync(ownerId, note.Id, CancellationToken.None));
    }
}