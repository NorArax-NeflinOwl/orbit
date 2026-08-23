using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Core.Notes.GetNoteById;
using Xunit;

namespace Orbit.Api.Tests.Notes;

public sealed class GetNoteByIdQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_the_note_when_owned_by_the_requesting_user()
    {
        var noteRepository = new InMemoryNoteRepository();
        var handler = new GetNoteByIdQueryHandler(
            new NoteAccessResolver(noteRepository, new InMemoryNoteShareRepository(), new InMemoryUserRepository()));
        var userId = Guid.NewGuid();
        var note = Note.Create(userId, "Title", [NoteContentLine.PlainText("Content")]);
        await noteRepository.AddAsync(note, CancellationToken.None);

        var result = await handler.HandleAsync(new GetNoteByIdQuery(userId, note.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(note.Id, result!.Id);
        Assert.False(result.IsShared);
        Assert.Equal(ShareAccessLevel.CanEdit, result.AccessLevel);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_a_note_neither_owned_by_nor_shared_with_the_requesting_user()
    {
        var noteRepository = new InMemoryNoteRepository();
        var handler = new GetNoteByIdQueryHandler(
            new NoteAccessResolver(noteRepository, new InMemoryNoteShareRepository(), new InMemoryUserRepository()));
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Title", [NoteContentLine.PlainText("Content")]);
        await noteRepository.AddAsync(note, CancellationToken.None);

        var result = await handler.HandleAsync(new GetNoteByIdQuery(otherUserId, note.Id), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_unknown_note_id()
    {
        var handler = new GetNoteByIdQueryHandler(
            new NoteAccessResolver(new InMemoryNoteRepository(), new InMemoryNoteShareRepository(), new InMemoryUserRepository()));

        var result = await handler.HandleAsync(new GetNoteByIdQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_returns_the_note_with_access_context_when_shared_via_an_accepted_grant()
    {
        var noteRepository = new InMemoryNoteRepository();
        var noteShareRepository = new InMemoryNoteShareRepository();
        var userRepository = new InMemoryUserRepository();
        var handler = new GetNoteByIdQueryHandler(new NoteAccessResolver(noteRepository, noteShareRepository, userRepository));

        var owner = Orbit.Core.Users.User.Create("owner@example.com", "owner", "Owner", "hash");
        await userRepository.AddAsync(owner, CancellationToken.None);
        var recipientId = Guid.NewGuid();
        var note = Note.Create(owner.Id, "Title", [NoteContentLine.PlainText("Content")]);
        await noteRepository.AddAsync(note, CancellationToken.None);
        var share = NoteShare.Create(note.Id, owner.Id, recipientId, ShareAccessLevel.CanEdit);
        share.MarkAccepted();
        await noteShareRepository.AddAsync(share, CancellationToken.None);

        var result = await handler.HandleAsync(new GetNoteByIdQuery(recipientId, note.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsShared);
        Assert.Equal("owner", result.SharedByUserName);
        Assert.Equal(ShareAccessLevel.CanEdit, result.AccessLevel);
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_a_share_exists_but_has_not_been_accepted_yet()
    {
        var noteRepository = new InMemoryNoteRepository();
        var noteShareRepository = new InMemoryNoteShareRepository();
        var handler = new GetNoteByIdQueryHandler(new NoteAccessResolver(noteRepository, noteShareRepository, new InMemoryUserRepository()));
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Title", [NoteContentLine.PlainText("Content")]);
        await noteRepository.AddAsync(note, CancellationToken.None);
        await noteShareRepository.AddAsync(NoteShare.Create(note.Id, ownerId, recipientId), CancellationToken.None);

        var result = await handler.HandleAsync(new GetNoteByIdQuery(recipientId, note.Id), CancellationToken.None);

        Assert.Null(result);
    }
}
