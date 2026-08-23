using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Core.Notes.ShareNote;
using Xunit;

namespace Orbit.Api.Tests.Notes;

public sealed class ShareNoteCommandHandlerTests
{
    private static ShareNoteCommandHandler CreateHandler(InMemoryNoteRepository noteRepository, InMemoryNoteShareRepository noteShareRepository)
        => new(new NoteAccessResolver(noteRepository, noteShareRepository, new InMemoryUserRepository()), noteShareRepository);

    [Fact]
    public async Task HandleAsync_creates_a_share_for_a_note_owned_by_the_requesting_user()
    {
        var noteRepository = new InMemoryNoteRepository();
        var shareRepository = new InMemoryNoteShareRepository();
        var handler = CreateHandler(noteRepository, shareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Shopping list", [NoteContentLine.PlainText("Milk, eggs")]);
        await noteRepository.AddAsync(note, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new ShareNoteCommand(ownerId, note.Id, recipientId, ShareAccessLevel.CanEdit), CancellationToken.None);

        Assert.NotNull(outcome);
        Assert.False(outcome!.AlreadyShared);
        var share = await shareRepository.GetByIdAsync(recipientId, outcome.ShareId, CancellationToken.None);
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
        var handler = CreateHandler(noteRepository, new InMemoryNoteShareRepository());
        var ownerId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Title", [NoteContentLine.PlainText("Content")]);
        await noteRepository.AddAsync(note, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new ShareNoteCommand(Guid.NewGuid(), note.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_unknown_note_id()
    {
        var handler = CreateHandler(new InMemoryNoteRepository(), new InMemoryNoteShareRepository());

        var outcome = await handler.HandleAsync(
            new ShareNoteCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_sharing_back_to_the_owner()
    {
        var noteRepository = new InMemoryNoteRepository();
        var handler = CreateHandler(noteRepository, new InMemoryNoteShareRepository());
        var ownerId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Title", [NoteContentLine.PlainText("Content")]);
        await noteRepository.AddAsync(note, CancellationToken.None);

        var outcome = await handler.HandleAsync(new ShareNoteCommand(ownerId, note.Id, ownerId), CancellationToken.None);

        Assert.Null(outcome);
    }

    /// <summary>Sets up noteRepository/noteShareRepository so recipientId has an accepted grant of accessLevel on a note owned by a fresh owner id.</summary>
    private static async Task<(Guid OwnerId, Guid RecipientId, Note Note)> ShareNoteWithAcceptedGrantAsync(
        InMemoryNoteRepository noteRepository, InMemoryNoteShareRepository noteShareRepository, ShareAccessLevel accessLevel)
    {
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Title", [NoteContentLine.PlainText("Content")]);
        await noteRepository.AddAsync(note, CancellationToken.None);
        var grant = NoteShare.Create(note.Id, ownerId, recipientId, accessLevel);
        grant.MarkAccepted();
        await noteShareRepository.AddAsync(grant, CancellationToken.None);
        return (ownerId, recipientId, note);
    }

    [Fact]
    public async Task HandleAsync_lets_a_CanEdit_recipient_re_share_at_any_level_except_back_to_the_owner()
    {
        var noteRepository = new InMemoryNoteRepository();
        var noteShareRepository = new InMemoryNoteShareRepository();
        var (_, recipientId, note) = await ShareNoteWithAcceptedGrantAsync(noteRepository, noteShareRepository, ShareAccessLevel.CanEdit);
        var handler = CreateHandler(noteRepository, noteShareRepository);

        var outcome = await handler.HandleAsync(
            new ShareNoteCommand(recipientId, note.Id, Guid.NewGuid(), ShareAccessLevel.CanEdit), CancellationToken.None);

        Assert.NotNull(outcome);
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_a_ReadOnly_recipient_tries_to_re_share()
    {
        var noteRepository = new InMemoryNoteRepository();
        var noteShareRepository = new InMemoryNoteShareRepository();
        var (_, recipientId, note) = await ShareNoteWithAcceptedGrantAsync(noteRepository, noteShareRepository, ShareAccessLevel.ReadOnly);
        var handler = CreateHandler(noteRepository, noteShareRepository);

        var outcome = await handler.HandleAsync(new ShareNoteCommand(recipientId, note.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task HandleAsync_lets_a_Share_tier_recipient_re_share_at_ReadOnly_or_Share_but_not_CanEdit()
    {
        var noteRepository = new InMemoryNoteRepository();
        var noteShareRepository = new InMemoryNoteShareRepository();
        var (_, recipientId, note) = await ShareNoteWithAcceptedGrantAsync(noteRepository, noteShareRepository, ShareAccessLevel.Share);
        var handler = CreateHandler(noteRepository, noteShareRepository);

        var readOnlyOutcome = await handler.HandleAsync(
            new ShareNoteCommand(recipientId, note.Id, Guid.NewGuid(), ShareAccessLevel.ReadOnly), CancellationToken.None);
        var shareOutcome = await handler.HandleAsync(
            new ShareNoteCommand(recipientId, note.Id, Guid.NewGuid(), ShareAccessLevel.Share), CancellationToken.None);
        var canEditOutcome = await handler.HandleAsync(
            new ShareNoteCommand(recipientId, note.Id, Guid.NewGuid(), ShareAccessLevel.CanEdit), CancellationToken.None);

        Assert.NotNull(readOnlyOutcome);
        Assert.NotNull(shareOutcome);
        Assert.Null(canEditOutcome);
    }

    [Fact]
    public async Task HandleAsync_reuses_an_existing_offer_to_the_same_recipient_instead_of_creating_a_duplicate()
    {
        var noteRepository = new InMemoryNoteRepository();
        var shareRepository = new InMemoryNoteShareRepository();
        var handler = CreateHandler(noteRepository, shareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Title", [NoteContentLine.PlainText("Content")]);
        await noteRepository.AddAsync(note, CancellationToken.None);

        var firstOutcome = await handler.HandleAsync(new ShareNoteCommand(ownerId, note.Id, recipientId), CancellationToken.None);
        var secondOutcome = await handler.HandleAsync(new ShareNoteCommand(ownerId, note.Id, recipientId), CancellationToken.None);

        Assert.NotNull(firstOutcome);
        Assert.False(firstOutcome!.AlreadyShared);
        Assert.NotNull(secondOutcome);
        Assert.True(secondOutcome!.AlreadyShared);
        Assert.Equal(firstOutcome.ShareId, secondOutcome.ShareId);
    }
}
