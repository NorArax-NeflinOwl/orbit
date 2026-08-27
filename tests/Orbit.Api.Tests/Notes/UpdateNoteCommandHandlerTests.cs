using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Core.Notes.UpdateNote;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Notes;

public sealed class UpdateNoteCommandHandlerTests
{
    private static UpdateNoteCommandHandler CreateHandler(
        InMemoryNoteRepository noteRepository, InMemoryNoteShareRepository? noteShareRepository = null, InMemoryUserRepository? userRepository = null)
        => new(
            new NoteAccessResolver(noteRepository, noteShareRepository ?? new InMemoryNoteShareRepository(), userRepository ?? new InMemoryUserRepository()),
            noteRepository);

    [Fact]
    public async Task HandleAsync_changes_the_priority_along_with_the_rest()
    {
        var repository = new InMemoryNoteRepository();
        var userId = Guid.NewGuid();
        var note = Note.Create(userId, "Title", [NoteContentLine.PlainText("Content")], priority: ItemPriority.Low);
        await repository.AddAsync(note, CancellationToken.None);

        await CreateHandler(repository).HandleAsync(
            new UpdateNoteCommand(
                userId, note.Id, "Title", [NoteContentLine.PlainText("Content")], IsPrivate: false,
                EncryptedContent: null, ItemPriority.High),
            CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, note.Id, CancellationToken.None);
        Assert.Equal(ItemPriority.High, stored!.Priority);
    }

    [Fact]
    public async Task HandleAsync_updates_a_note_owned_by_the_requesting_user()
    {
        var repository = new InMemoryNoteRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var note = Note.Create(userId, "Original title", [NoteContentLine.PlainText("Original content")]);
        await repository.AddAsync(note, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new UpdateNoteCommand(userId, note.Id, "New title", [NoteContentLine.PlainText("New content")], IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        var stored = await repository.GetByIdAsync(userId, note.Id, CancellationToken.None);
        Assert.Equal("New title", stored!.Title);
        Assert.Equal("New content", Assert.Single(stored.Content).Text);
    }

    [Fact]
    public async Task HandleAsync_returns_NotFound_and_does_not_update_a_note_owned_by_a_different_user()
    {
        var repository = new InMemoryNoteRepository();
        var handler = CreateHandler(repository);
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Original title", [NoteContentLine.PlainText("Original content")]);
        await repository.AddAsync(note, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new UpdateNoteCommand(otherUserId, note.Id, "Hijacked title", [NoteContentLine.PlainText("Hijacked content")], IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.NotFound, outcome.Kind);
        var stored = await repository.GetByIdAsync(ownerId, note.Id, CancellationToken.None);
        Assert.Equal("Original title", stored!.Title);
    }

    private async Task<(InMemoryNoteRepository NoteRepository, Guid OwnerId, Guid RecipientId, Note Note)> CreateSharedNoteAsync(
        InMemoryNoteShareRepository noteShareRepository, ShareAccessLevel accessLevel)
    {
        var noteRepository = new InMemoryNoteRepository();
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var note = Note.Create(ownerId, "Original title", [NoteContentLine.PlainText("Original content")]);
        await noteRepository.AddAsync(note, CancellationToken.None);
        var share = NoteShare.Create(note.Id, ownerId, recipientId, accessLevel);
        share.MarkAccepted();
        await noteShareRepository.AddAsync(share, CancellationToken.None);
        return (noteRepository, ownerId, recipientId, note);
    }

    [Fact]
    public async Task HandleAsync_returns_ReadOnly_and_does_not_update_a_shared_read_only_note()
    {
        var noteShareRepository = new InMemoryNoteShareRepository();
        var (noteRepository, _, recipientId, note) = await CreateSharedNoteAsync(noteShareRepository, ShareAccessLevel.ReadOnly);
        var handler = CreateHandler(noteRepository, noteShareRepository);

        var outcome = await handler.HandleAsync(
            new UpdateNoteCommand(recipientId, note.Id, "Edited title", [NoteContentLine.PlainText("Edited content")], IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.ReadOnly, outcome.Kind);
    }

    [Fact]
    public async Task HandleAsync_returns_ReadOnly_and_does_not_update_a_note_shared_at_the_Share_tier()
    {
        var noteShareRepository = new InMemoryNoteShareRepository();
        var (noteRepository, _, recipientId, note) = await CreateSharedNoteAsync(noteShareRepository, ShareAccessLevel.Share);
        var handler = CreateHandler(noteRepository, noteShareRepository);

        var outcome = await handler.HandleAsync(
            new UpdateNoteCommand(recipientId, note.Id, "Edited title", [NoteContentLine.PlainText("Edited content")], IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.ReadOnly, outcome.Kind);
    }

    [Fact]
    public async Task HandleAsync_updates_a_shared_note_with_edit_access()
    {
        var noteShareRepository = new InMemoryNoteShareRepository();
        var (noteRepository, ownerId, recipientId, note) = await CreateSharedNoteAsync(noteShareRepository, ShareAccessLevel.CanEdit);
        var handler = CreateHandler(noteRepository, noteShareRepository);

        var outcome = await handler.HandleAsync(
            new UpdateNoteCommand(recipientId, note.Id, "New title", [NoteContentLine.PlainText("New content")], IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        var stored = await noteRepository.GetByIdAsync(ownerId, note.Id, CancellationToken.None);
        Assert.Equal("New title", stored!.Title);
    }

    [Fact]
    public async Task HandleAsync_returns_Locked_when_someone_else_holds_the_edit_lock()
    {
        var repository = new InMemoryNoteRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var note = Note.Create(userId, "Original title", [NoteContentLine.PlainText("Original content")]);
        note.AcquireLock(otherUserId, "otherUser", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        await repository.AddAsync(note, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new UpdateNoteCommand(userId, note.Id, "Edited title", [NoteContentLine.PlainText("Edited content")], IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Locked, outcome.Kind);
        Assert.Equal("otherUser", outcome.LockedByUserName);
    }

    [Fact]
    public async Task HandleAsync_updates_a_note_the_caller_themselves_holds_the_lock_on()
    {
        var repository = new InMemoryNoteRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var note = Note.Create(userId, "Original title", [NoteContentLine.PlainText("Original content")]);
        note.AcquireLock(userId, "me", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        await repository.AddAsync(note, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new UpdateNoteCommand(userId, note.Id, "New title", [NoteContentLine.PlainText("New content")], IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
    }

    [Fact]
    public async Task HandleAsync_updates_a_note_whose_lock_has_expired()
    {
        var repository = new InMemoryNoteRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var note = Note.Create(userId, "Original title", [NoteContentLine.PlainText("Original content")]);
        note.AcquireLock(otherUserId, "otherUser", DateTimeOffset.UtcNow.AddMinutes(-5), TimeSpan.FromMinutes(1));
        await repository.AddAsync(note, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new UpdateNoteCommand(userId, note.Id, "New title", [NoteContentLine.PlainText("New content")], IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
    }

    [Fact]
    public async Task HandleAsync_returns_NotFound_for_an_unknown_note_id()
    {
        var handler = CreateHandler(new InMemoryNoteRepository());

        var outcome = await handler.HandleAsync(
            new UpdateNoteCommand(Guid.NewGuid(), Guid.NewGuid(), "Title", [NoteContentLine.PlainText("Content")], IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.NotFound, outcome.Kind);
    }
}
