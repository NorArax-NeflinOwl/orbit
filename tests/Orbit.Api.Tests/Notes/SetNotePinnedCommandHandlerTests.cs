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
        var handler = new SetNotePinnedCommandHandler(noteRepository, new InMemoryNoteShareRepository());

        var pinned = await handler.HandleAsync(new SetNotePinnedCommand(ownerId, note.Id, IsPinned: true), CancellationToken.None);

        Assert.True(pinned);
        Assert.True((await noteRepository.GetByIdAsync(ownerId, note.Id, CancellationToken.None))!.IsPinned);
    }

    [Fact]
    public async Task HandleAsync_refuses_somebody_who_neither_owns_the_note_nor_holds_a_grant()
    {
        var noteRepository = new InMemoryNoteRepository();
        var ownerId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Shopping list", [NoteContentLine.PlainText("Milk, eggs")]);
        await noteRepository.AddAsync(note, CancellationToken.None);
        var handler = new SetNotePinnedCommandHandler(noteRepository, new InMemoryNoteShareRepository());

        // Nothing on their page to arrange: they cannot see this note at all.
        var pinned = await handler.HandleAsync(
            new SetNotePinnedCommand(Guid.NewGuid(), note.Id, IsPinned: true), CancellationToken.None);

        Assert.False(pinned);
        Assert.False((await noteRepository.GetByIdAsync(ownerId, note.Id, CancellationToken.None))!.IsPinned);
    }

    /// <summary>
    /// A recipient's answer goes on their own grant, and the note the owner holds is left exactly as it
    /// was. This used to be refused outright, and the browser kept the answer in localStorage instead -
    /// where it did not follow the reader to a second browser or to their phone.
    /// </summary>
    [Fact]
    public async Task HandleAsync_pins_a_shared_note_on_the_recipients_own_grant()
    {
        var noteRepository = new InMemoryNoteRepository();
        var shareRepository = new InMemoryNoteShareRepository();
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Shopping list", [NoteContentLine.PlainText("Milk, eggs")]);
        await noteRepository.AddAsync(note, CancellationToken.None);
        var grant = NoteShare.Create(note.Id, ownerId, recipientId);
        grant.MarkAccepted();
        await shareRepository.AddAsync(grant, CancellationToken.None);
        var handler = new SetNotePinnedCommandHandler(noteRepository, shareRepository);

        var pinned = await handler.HandleAsync(
            new SetNotePinnedCommand(recipientId, note.Id, IsPinned: true), CancellationToken.None);

        Assert.True(pinned);
        Assert.True(
            (await shareRepository.FindAcceptedGrantAsync(note.Id, recipientId, CancellationToken.None))!
                .IsPinnedByRecipient);
        // The owner's own page is untouched - which is the whole reason this does not write to the note.
        Assert.False((await noteRepository.GetByIdAsync(ownerId, note.Id, CancellationToken.None))!.IsPinned);
    }

    /// <summary>
    /// An offer nobody has taken up is not access. Until it is accepted there is no card on the
    /// recipient's page for a pin to be about.
    /// </summary>
    [Fact]
    public async Task HandleAsync_refuses_a_recipient_whose_share_is_still_only_an_offer()
    {
        var noteRepository = new InMemoryNoteRepository();
        var shareRepository = new InMemoryNoteShareRepository();
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Shopping list", [NoteContentLine.PlainText("Milk, eggs")]);
        await noteRepository.AddAsync(note, CancellationToken.None);
        await shareRepository.AddAsync(NoteShare.Create(note.Id, ownerId, recipientId), CancellationToken.None);
        var handler = new SetNotePinnedCommandHandler(noteRepository, shareRepository);

        var pinned = await handler.HandleAsync(
            new SetNotePinnedCommand(recipientId, note.Id, IsPinned: true), CancellationToken.None);

        Assert.False(pinned);
    }

    [Fact]
    public async Task HandleAsync_leaves_the_note_untouched_apart_from_the_pin()
    {
        var noteRepository = new InMemoryNoteRepository();
        var ownerId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Shopping list", [NoteContentLine.PlainText("Milk, eggs")]);
        await noteRepository.AddAsync(note, CancellationToken.None);
        var updatedBefore = note.UpdatedAtUtc;
        var handler = new SetNotePinnedCommandHandler(noteRepository, new InMemoryNoteShareRepository());

        await handler.HandleAsync(new SetNotePinnedCommand(ownerId, note.Id, IsPinned: true), CancellationToken.None);

        // Pinning moves a card on a page; it does not touch what the note says, so it must not make the
        // note look freshly edited to everyone it is shared with.
        var stored = (await noteRepository.GetByIdAsync(ownerId, note.Id, CancellationToken.None))!;
        Assert.Equal(updatedBefore, stored.UpdatedAtUtc);
        Assert.Equal("Shopping list", stored.Title);
    }
}
