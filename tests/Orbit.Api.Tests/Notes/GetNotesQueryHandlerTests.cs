using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Core.Notes.GetNotes;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Notes;

public sealed class GetNotesQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_only_notes_owned_by_the_requesting_user()
    {
        var noteRepository = new InMemoryNoteRepository();
        var handler = new GetNotesQueryHandler(
            new NoteAccessResolver(noteRepository, new InMemoryNoteShareRepository(), new InMemoryUserRepository()));
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await noteRepository.AddAsync(Note.Create(userId, "Mine", [NoteContentLine.PlainText("Content")]), CancellationToken.None);
        await noteRepository.AddAsync(Note.Create(otherUserId, "Not mine", [NoteContentLine.PlainText("Content")]), CancellationToken.None);

        var notes = await handler.HandleAsync(new GetNotesQuery(userId), CancellationToken.None);

        var note = Assert.Single(notes);
        Assert.Equal("Mine", note.Title);
    }

    [Fact]
    public async Task HandleAsync_returns_an_empty_list_when_the_user_has_no_notes()
    {
        var handler = new GetNotesQueryHandler(
            new NoteAccessResolver(new InMemoryNoteRepository(), new InMemoryNoteShareRepository(), new InMemoryUserRepository()));

        var notes = await handler.HandleAsync(new GetNotesQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(notes);
    }

    [Fact]
    public async Task HandleAsync_includes_notes_shared_via_an_accepted_grant_alongside_owned_notes()
    {
        var noteRepository = new InMemoryNoteRepository();
        var noteShareRepository = new InMemoryNoteShareRepository();
        var userRepository = new InMemoryUserRepository();
        var handler = new GetNotesQueryHandler(new NoteAccessResolver(noteRepository, noteShareRepository, userRepository));

        var owner = User.Create("owner@example.com", "owner", "Owner", "hash");
        await userRepository.AddAsync(owner, CancellationToken.None);
        var recipientId = Guid.NewGuid();
        await noteRepository.AddAsync(Note.Create(recipientId, "Mine", [NoteContentLine.PlainText("Content")]), CancellationToken.None);
        var sharedNote = Note.Create(owner.Id, "Shared with me", [NoteContentLine.PlainText("Content")]);
        await noteRepository.AddAsync(sharedNote, CancellationToken.None);
        var share = NoteShare.Create(sharedNote.Id, owner.Id, recipientId, ShareAccessLevel.ReadOnly);
        share.MarkAccepted();
        await noteShareRepository.AddAsync(share, CancellationToken.None);

        var notes = await handler.HandleAsync(new GetNotesQuery(recipientId), CancellationToken.None);

        Assert.Equal(2, notes.Count);
        var shared = Assert.Single(notes, note => note.Title == "Shared with me");
        Assert.True(shared.IsShared);
        Assert.Equal(ShareAccessLevel.ReadOnly, shared.AccessLevel);
    }
}
