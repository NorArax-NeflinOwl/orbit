using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Core.Notes.ShareNote;
using Xunit;

namespace Orbit.Api.Tests.Notes;

public sealed class ShareNoteCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_creates_a_share_for_a_note_owned_by_the_requesting_user()
    {
        var noteRepository = new InMemoryNoteRepository();
        var shareRepository = new InMemoryNoteShareRepository();
        var handler = new ShareNoteCommandHandler(noteRepository, shareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Shopping list", "Milk, eggs");
        await noteRepository.AddAsync(note, CancellationToken.None);

        var shareId = await handler.HandleAsync(
            new ShareNoteCommand(ownerId, note.Id, recipientId, ShareAccessLevel.CanEdit), CancellationToken.None);

        Assert.NotNull(shareId);
        var share = await shareRepository.GetByIdAsync(recipientId, shareId!.Value, CancellationToken.None);
        Assert.NotNull(share);
        Assert.Equal(note.Id, share!.SourceNoteId);
        Assert.Equal(ownerId, share.OwnerUserId);
        Assert.Equal(recipientId, share.RecipientUserId);
        Assert.Equal(ShareAccessLevel.CanEdit, share.AccessLevel);
        Assert.False(share.IsAccepted);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_a_note_not_owned_by_the_requesting_user()
    {
        var noteRepository = new InMemoryNoteRepository();
        var handler = new ShareNoteCommandHandler(noteRepository, new InMemoryNoteShareRepository());
        var ownerId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Title", "Content");
        await noteRepository.AddAsync(note, CancellationToken.None);

        var shareId = await handler.HandleAsync(
            new ShareNoteCommand(Guid.NewGuid(), note.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Null(shareId);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_unknown_note_id()
    {
        var handler = new ShareNoteCommandHandler(new InMemoryNoteRepository(), new InMemoryNoteShareRepository());

        var shareId = await handler.HandleAsync(
            new ShareNoteCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(shareId);
    }
}
