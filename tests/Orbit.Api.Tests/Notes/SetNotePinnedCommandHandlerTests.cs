using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Notes;
using Orbit.Core.Notes.SetNotePinned;
using Xunit;

namespace Orbit.Api.Tests.Notes;

public sealed class SetNotePinnedCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_pins_a_note_its_owner_asked_to_pin()
    {
        var noteRepository = new InMemoryNoteRepository();
        var ownerId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Shopping list", [NoteContentLine.PlainText("Milk, eggs")]);
        await noteRepository.AddAsync(note, CancellationToken.None);
        var handler = new SetNotePinnedCommandHandler(noteRepository);

        var pinned = await handler.HandleAsync(new SetNotePinnedCommand(ownerId, note.Id, IsPinned: true), CancellationToken.None);

        Assert.True(pinned);
        Assert.True((await noteRepository.GetByIdAsync(ownerId, note.Id, CancellationToken.None))!.IsPinned);
    }

    [Fact]
    public async Task HandleAsync_refuses_to_pin_somebody_elses_note()
    {
        var noteRepository = new InMemoryNoteRepository();
        var ownerId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Shopping list", [NoteContentLine.PlainText("Milk, eggs")]);
        await noteRepository.AddAsync(note, CancellationToken.None);
        var handler = new SetNotePinnedCommandHandler(noteRepository);

        // A recipient pinning a note shared with them would move it on its owner's page, not their own.
        var pinned = await handler.HandleAsync(
            new SetNotePinnedCommand(Guid.NewGuid(), note.Id, IsPinned: true), CancellationToken.None);

        Assert.False(pinned);
        Assert.False((await noteRepository.GetByIdAsync(ownerId, note.Id, CancellationToken.None))!.IsPinned);
    }

    [Fact]
    public async Task HandleAsync_leaves_the_note_untouched_apart_from_the_pin()
    {
        var noteRepository = new InMemoryNoteRepository();
        var ownerId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Shopping list", [NoteContentLine.PlainText("Milk, eggs")]);
        await noteRepository.AddAsync(note, CancellationToken.None);
        var updatedBefore = note.UpdatedAtUtc;
        var handler = new SetNotePinnedCommandHandler(noteRepository);

        await handler.HandleAsync(new SetNotePinnedCommand(ownerId, note.Id, IsPinned: true), CancellationToken.None);

        // Pinning moves a card on a page; it does not touch what the note says, so it must not make the
        // note look freshly edited to everyone it is shared with.
        var stored = (await noteRepository.GetByIdAsync(ownerId, note.Id, CancellationToken.None))!;
        Assert.Equal(updatedBefore, stored.UpdatedAtUtc);
        Assert.Equal("Shopping list", stored.Title);
    }
}
