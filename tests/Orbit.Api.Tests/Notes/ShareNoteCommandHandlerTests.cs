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
        Assert.Equal(ownerId, share.OriginalOwnerUserId);
        Assert.False(share.IsAccepted);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_a_note_not_owned_by_the_requesting_user()
    {
        var noteRepository = new InMemoryNoteRepository();
        var handler = new ShareNoteCommandHandler(noteRepository, new InMemoryNoteShareRepository());
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
        var handler = new ShareNoteCommandHandler(new InMemoryNoteRepository(), new InMemoryNoteShareRepository());

        var outcome = await handler.HandleAsync(
            new ShareNoteCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_sharing_back_to_the_original_owner()
    {
        var noteRepository = new InMemoryNoteRepository();
        var handler = new ShareNoteCommandHandler(noteRepository, new InMemoryNoteShareRepository());
        var ownerId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Title", [NoteContentLine.PlainText("Content")]);
        await noteRepository.AddAsync(note, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new ShareNoteCommand(ownerId, note.Id, ownerId), CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task HandleAsync_lets_a_CanEdit_recipient_re_share_at_any_level_except_back_to_the_original_owner()
    {
        var noteRepository = new InMemoryNoteRepository();
        var handler = new ShareNoteCommandHandler(noteRepository, new InMemoryNoteShareRepository());
        var originalOwnerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var thirdPartyId = Guid.NewGuid();
        var sharedCopy = Note.CreateShared(recipientId, "Title", [], "owner", ShareAccessLevel.CanEdit, originalOwnerId);
        await noteRepository.AddAsync(sharedCopy, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new ShareNoteCommand(recipientId, sharedCopy.Id, thirdPartyId, ShareAccessLevel.CanEdit), CancellationToken.None);

        Assert.NotNull(outcome);
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_a_ReadOnly_recipient_tries_to_re_share()
    {
        var noteRepository = new InMemoryNoteRepository();
        var handler = new ShareNoteCommandHandler(noteRepository, new InMemoryNoteShareRepository());
        var originalOwnerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var sharedCopy = Note.CreateShared(recipientId, "Title", [], "owner", ShareAccessLevel.ReadOnly, originalOwnerId);
        await noteRepository.AddAsync(sharedCopy, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new ShareNoteCommand(recipientId, sharedCopy.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task HandleAsync_lets_a_Share_tier_recipient_re_share_at_ReadOnly_or_Share_but_not_CanEdit()
    {
        var noteRepository = new InMemoryNoteRepository();
        var handler = new ShareNoteCommandHandler(noteRepository, new InMemoryNoteShareRepository());
        var originalOwnerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var sharedCopy = Note.CreateShared(recipientId, "Title", [], "owner", ShareAccessLevel.Share, originalOwnerId);
        await noteRepository.AddAsync(sharedCopy, CancellationToken.None);

        var readOnlyOutcome = await handler.HandleAsync(
            new ShareNoteCommand(recipientId, sharedCopy.Id, Guid.NewGuid(), ShareAccessLevel.ReadOnly), CancellationToken.None);
        var shareOutcome = await handler.HandleAsync(
            new ShareNoteCommand(recipientId, sharedCopy.Id, Guid.NewGuid(), ShareAccessLevel.Share), CancellationToken.None);
        var canEditOutcome = await handler.HandleAsync(
            new ShareNoteCommand(recipientId, sharedCopy.Id, Guid.NewGuid(), ShareAccessLevel.CanEdit), CancellationToken.None);

        Assert.NotNull(readOnlyOutcome);
        Assert.NotNull(shareOutcome);
        Assert.Null(canEditOutcome);
    }

    [Fact]
    public async Task HandleAsync_reuses_an_existing_offer_to_the_same_recipient_instead_of_creating_a_duplicate()
    {
        var noteRepository = new InMemoryNoteRepository();
        var shareRepository = new InMemoryNoteShareRepository();
        var handler = new ShareNoteCommandHandler(noteRepository, shareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Title", [NoteContentLine.PlainText("Content")]);
        await noteRepository.AddAsync(note, CancellationToken.None);

        var firstOutcome = await handler.HandleAsync(
            new ShareNoteCommand(ownerId, note.Id, recipientId), CancellationToken.None);
        var secondOutcome = await handler.HandleAsync(
            new ShareNoteCommand(ownerId, note.Id, recipientId), CancellationToken.None);

        Assert.NotNull(firstOutcome);
        Assert.False(firstOutcome!.AlreadyShared);
        Assert.NotNull(secondOutcome);
        Assert.True(secondOutcome!.AlreadyShared);
        Assert.Equal(firstOutcome.ShareId, secondOutcome.ShareId);
    }
}
