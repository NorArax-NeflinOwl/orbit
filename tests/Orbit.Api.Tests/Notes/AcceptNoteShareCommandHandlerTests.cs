using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Core.Notes.AcceptNoteShare;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Notes;

public sealed class AcceptNoteShareCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_creates_a_copy_in_the_recipients_notes()
    {
        var noteRepository = new InMemoryNoteRepository();
        var shareRepository = new InMemoryNoteShareRepository();
        var userRepository = new InMemoryUserRepository();
        var handler = new AcceptNoteShareCommandHandler(shareRepository, noteRepository, userRepository);

        var owner = User.Create("owner@example.com", "owner", "Owner", "hash");
        await userRepository.AddAsync(owner, CancellationToken.None);
        var recipientId = Guid.NewGuid();
        var sourceNote = Note.Create(owner.Id, "Shopping list", [NoteContentLine.PlainText("Milk, eggs")]);
        await noteRepository.AddAsync(sourceNote, CancellationToken.None);
        var share = NoteShare.Create(sourceNote.Id, owner.Id, recipientId, owner.Id, ShareAccessLevel.CanEdit);
        await shareRepository.AddAsync(share, CancellationToken.None);

        var accepted = await handler.HandleAsync(new AcceptNoteShareCommand(recipientId, share.Id), CancellationToken.None);

        Assert.True(accepted);
        var recipientNotes = await noteRepository.GetAllAsync(recipientId, CancellationToken.None);
        var sharedCopy = Assert.Single(recipientNotes);
        Assert.True(sharedCopy.IsShared);
        Assert.Equal("owner", sharedCopy.SharedByUserName);
        Assert.Equal(ShareAccessLevel.CanEdit, sharedCopy.AccessLevel);
        Assert.Equal(owner.Id, sharedCopy.OriginalOwnerUserId);
        Assert.Equal("Shopping list", sharedCopy.Title);
    }

    [Fact]
    public async Task HandleAsync_returns_false_when_the_share_was_not_offered_to_the_requesting_user()
    {
        var noteRepository = new InMemoryNoteRepository();
        var shareRepository = new InMemoryNoteShareRepository();
        var userRepository = new InMemoryUserRepository();
        var handler = new AcceptNoteShareCommandHandler(shareRepository, noteRepository, userRepository);

        var owner = User.Create("owner@example.com", "owner", "Owner", "hash");
        await userRepository.AddAsync(owner, CancellationToken.None);
        var sourceNote = Note.Create(owner.Id, "Title", [NoteContentLine.PlainText("Content")]);
        await noteRepository.AddAsync(sourceNote, CancellationToken.None);
        var share = NoteShare.Create(sourceNote.Id, owner.Id, Guid.NewGuid(), owner.Id);
        await shareRepository.AddAsync(share, CancellationToken.None);

        var accepted = await handler.HandleAsync(
            new AcceptNoteShareCommand(Guid.NewGuid(), share.Id), CancellationToken.None);

        Assert.False(accepted);
    }

    [Fact]
    public async Task HandleAsync_returns_false_for_an_unknown_share_id()
    {
        var handler = new AcceptNoteShareCommandHandler(
            new InMemoryNoteShareRepository(), new InMemoryNoteRepository(), new InMemoryUserRepository());

        var accepted = await handler.HandleAsync(
            new AcceptNoteShareCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(accepted);
    }

    [Fact]
    public async Task HandleAsync_is_idempotent_and_does_not_create_a_second_copy_when_accepted_twice()
    {
        var noteRepository = new InMemoryNoteRepository();
        var shareRepository = new InMemoryNoteShareRepository();
        var userRepository = new InMemoryUserRepository();
        var handler = new AcceptNoteShareCommandHandler(shareRepository, noteRepository, userRepository);

        var owner = User.Create("owner@example.com", "owner", "Owner", "hash");
        await userRepository.AddAsync(owner, CancellationToken.None);
        var recipientId = Guid.NewGuid();
        var sourceNote = Note.Create(owner.Id, "Title", [NoteContentLine.PlainText("Content")]);
        await noteRepository.AddAsync(sourceNote, CancellationToken.None);
        var share = NoteShare.Create(sourceNote.Id, owner.Id, recipientId, owner.Id);
        await shareRepository.AddAsync(share, CancellationToken.None);

        var command = new AcceptNoteShareCommand(recipientId, share.Id);
        await handler.HandleAsync(command, CancellationToken.None);
        var acceptedAgain = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(acceptedAgain);
        var recipientNotes = await noteRepository.GetAllAsync(recipientId, CancellationToken.None);
        Assert.Single(recipientNotes);
    }
}
