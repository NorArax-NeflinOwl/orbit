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

    /// <summary>
    /// A shared note arrives carrying the *recipient's* pin, not its owner's. The owner's says where it
    /// sits on the owner's page, and handing that over unchanged put a note somebody else had pinned at
    /// the top of this reader's list - which is what the phone was doing, since it sorts by this flag
    /// and had no second answer to prefer. See NoteAccessResolver.
    /// </summary>
    [Fact]
    public async Task HandleAsync_hands_a_recipient_their_own_pin_rather_than_the_owners()
    {
        var noteRepository = new InMemoryNoteRepository();
        var noteShareRepository = new InMemoryNoteShareRepository();
        var userRepository = new InMemoryUserRepository();
        var handler = new GetNotesQueryHandler(new NoteAccessResolver(noteRepository, noteShareRepository, userRepository));

        var owner = User.Create("owner@example.com", "owner", "Owner", "hash");
        await userRepository.AddAsync(owner, CancellationToken.None);
        var recipientId = Guid.NewGuid();
        var sharedNote = Note.Create(owner.Id, "Shared with me", [NoteContentLine.PlainText("Content")]);
        sharedNote.SetPinned(true);
        await noteRepository.AddAsync(sharedNote, CancellationToken.None);
        var share = NoteShare.Create(sharedNote.Id, owner.Id, recipientId, ShareAccessLevel.ReadOnly);
        share.MarkAccepted();
        await noteShareRepository.AddAsync(share, CancellationToken.None);

        var notes = await handler.HandleAsync(new GetNotesQuery(recipientId), CancellationToken.None);

        Assert.False(Assert.Single(notes).IsPinnedForCaller);
    }

    /// <summary>And the recipient's own answer does reach them, which is what makes the pin worth having.</summary>
    [Fact]
    public async Task HandleAsync_hands_a_recipient_the_pin_they_set_themselves()
    {
        var noteRepository = new InMemoryNoteRepository();
        var noteShareRepository = new InMemoryNoteShareRepository();
        var userRepository = new InMemoryUserRepository();
        var handler = new GetNotesQueryHandler(new NoteAccessResolver(noteRepository, noteShareRepository, userRepository));

        var owner = User.Create("owner@example.com", "owner", "Owner", "hash");
        await userRepository.AddAsync(owner, CancellationToken.None);
        var recipientId = Guid.NewGuid();
        var sharedNote = Note.Create(owner.Id, "Shared with me", [NoteContentLine.PlainText("Content")]);
        await noteRepository.AddAsync(sharedNote, CancellationToken.None);
        var share = NoteShare.Create(sharedNote.Id, owner.Id, recipientId, ShareAccessLevel.ReadOnly);
        share.MarkAccepted();
        share.SetPinnedByRecipient(true);
        await noteShareRepository.AddAsync(share, CancellationToken.None);

        var notes = await handler.HandleAsync(new GetNotesQuery(recipientId), CancellationToken.None);

        Assert.True(Assert.Single(notes).IsPinnedForCaller);
    }

    /// <summary>
    /// And the stamp must not survive into a save. The resolver feeds the *write* paths too - a note is
    /// resolved before it is updated - so a recipient's pin written over the stored flag would have been
    /// saved onto the owner's row the next time that recipient changed a word, silently rearranging
    /// somebody else's page. That is why IsPinnedForCaller is a field of its own rather than an
    /// overwrite of IsPinned.
    /// </summary>
    [Fact]
    public async Task A_recipient_saving_a_shared_note_leaves_the_owners_pin_alone()
    {
        var noteRepository = new InMemoryNoteRepository();
        var noteShareRepository = new InMemoryNoteShareRepository();
        var userRepository = new InMemoryUserRepository();
        var resolver = new NoteAccessResolver(noteRepository, noteShareRepository, userRepository);

        var owner = User.Create("owner@example.com", "owner", "Owner", "hash");
        await userRepository.AddAsync(owner, CancellationToken.None);
        var recipientId = Guid.NewGuid();
        var sharedNote = Note.Create(owner.Id, "Shared with me", [NoteContentLine.PlainText("Content")]);
        sharedNote.SetPinned(true);
        await noteRepository.AddAsync(sharedNote, CancellationToken.None);
        var share = NoteShare.Create(sharedNote.Id, owner.Id, recipientId, ShareAccessLevel.CanEdit);
        share.MarkAccepted();
        await noteShareRepository.AddAsync(share, CancellationToken.None);

        // What UpdateNoteCommandHandler does: resolve for the caller, change something, save.
        var resolved = (await resolver.ResolveAsync(recipientId, sharedNote.Id, CancellationToken.None))!;
        resolved.Update("Shared with me", [NoteContentLine.PlainText("Milk")], isPrivate: false, null, ItemPriority.Normal);
        await noteRepository.UpdateAsync(resolved, CancellationToken.None);

        Assert.True((await noteRepository.GetByIdAsync(owner.Id, sharedNote.Id, CancellationToken.None))!.IsPinned);
    }
}
