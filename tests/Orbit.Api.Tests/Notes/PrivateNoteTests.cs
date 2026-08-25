using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Core.Notes.CreateNote;
using Orbit.Core.Notes.ShareNote;
using Orbit.Core.Notes.UpdateNote;
using Xunit;

namespace Orbit.Api.Tests.Notes;

/// <summary>
/// Covers the promise a private note makes: the server keeps nothing readable, nobody but the owner can
/// reach it, and it can't be shared - including a note that was already shared before being made private.
/// </summary>
public sealed class PrivateNoteTests
{
    private static readonly EncryptedPayload SealedContent = new("c2VhbGVk", "bm9uY2U=");

    [Fact]
    public async Task A_private_note_keeps_nothing_readable_on_the_server()
    {
        var context = new PrivateNoteTestContext();
        var lines = new[] { new NoteContentLine("Bank details", false, false) };

        var noteId = await context.CreateAsync("Passwords", lines, isPrivate: true, SealedContent);

        var stored = await context.NoteRepository.GetByIdAsync(context.OwnerId, noteId, CancellationToken.None);
        // Not merely hidden from responses: there is nothing left in the row to hide.
        Assert.Equal(string.Empty, stored!.Title);
        Assert.Empty(stored.Content);
        Assert.Equal(SealedContent, stored.EncryptedContent);
        Assert.True(stored.IsPrivate);
    }

    [Fact]
    public async Task An_ordinary_note_is_stored_readable_and_carries_no_sealed_content()
    {
        var context = new PrivateNoteTestContext();

        var noteId = await context.CreateAsync("Shopping", [new NoteContentLine("Milk", false, false)], isPrivate: false, encryptedContent: null);

        var stored = await context.NoteRepository.GetByIdAsync(context.OwnerId, noteId, CancellationToken.None);
        Assert.Equal("Shopping", stored!.Title);
        Assert.Null(stored.EncryptedContent);
        Assert.False(stored.IsPrivate);
    }

    [Fact]
    public async Task Claiming_privacy_without_sealed_content_is_refused()
    {
        var context = new PrivateNoteTestContext();

        // Otherwise a hand-made request could set the flag and still leave the content readable, which
        // is the one thing this feature must never allow.
        await Assert.ThrowsAsync<InvalidRequestException>(
            () => context.CreateAsync("Passwords", [new NoteContentLine("Secret", false, false)], isPrivate: true, encryptedContent: null));
    }

    [Fact]
    public async Task Turning_privacy_on_clears_the_content_that_was_readable_before()
    {
        var context = new PrivateNoteTestContext();
        var noteId = await context.CreateAsync("Shopping", [new NoteContentLine("Milk", false, false)], isPrivate: false, encryptedContent: null);

        var outcome = await context.UpdateAsync(noteId, "Shopping", [new NoteContentLine("Milk", false, false)], isPrivate: true, SealedContent);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        var stored = await context.NoteRepository.GetByIdAsync(context.OwnerId, noteId, CancellationToken.None);
        Assert.Equal(string.Empty, stored!.Title);
        Assert.Empty(stored.Content);
    }

    [Fact]
    public async Task Turning_privacy_back_off_drops_the_sealed_content()
    {
        var context = new PrivateNoteTestContext();
        var noteId = await context.CreateAsync("Passwords", [], isPrivate: true, SealedContent);

        await context.UpdateAsync(noteId, "Shopping", [new NoteContentLine("Milk", false, false)], isPrivate: false, encryptedContent: null);

        var stored = await context.NoteRepository.GetByIdAsync(context.OwnerId, noteId, CancellationToken.None);
        Assert.Equal("Shopping", stored!.Title);
        Assert.Null(stored.EncryptedContent);
        Assert.False(stored.IsPrivate);
    }

    [Fact]
    public async Task A_private_note_cannot_be_shared()
    {
        var context = new PrivateNoteTestContext();
        var noteId = await context.CreateAsync("Passwords", [], isPrivate: true, SealedContent);

        await Assert.ThrowsAsync<InvalidRequestException>(() => context.ShareAsync(noteId, Guid.NewGuid()));
    }

    [Fact]
    public async Task An_ordinary_shared_note_still_resolves_for_its_recipient()
    {
        // The control for the test below: without it, "the recipient sees nothing" could just as well
        // mean the sharing set-up never worked. Kept as its own test rather than an assertion inside
        // that one because the in-memory repository hands every caller the same Note instance, so
        // resolving as the recipient first leaves its access context stamped on the row the owner then
        // tries to edit - an artefact of the double, not of the code under test.
        var context = new PrivateNoteTestContext();
        var recipientId = Guid.NewGuid();
        var noteId = await context.CreateAsync("Shopping", [new NoteContentLine("Milk", false, false)], isPrivate: false, encryptedContent: null);
        await context.ShareAndAcceptAsync(noteId, recipientId);

        Assert.NotNull(await context.ResolveForAsync(recipientId, noteId));
    }

    [Fact]
    public async Task An_existing_share_stops_granting_access_once_the_note_becomes_private()
    {
        var context = new PrivateNoteTestContext();
        var recipientId = Guid.NewGuid();
        var noteId = await context.CreateAsync("Shopping", [new NoteContentLine("Milk", false, false)], isPrivate: false, encryptedContent: null);
        await context.ShareAndAcceptAsync(noteId, recipientId);

        await context.UpdateAsync(noteId, "Shopping", [], isPrivate: true, SealedContent);

        // The grant row is left alone; it simply stops resolving, so a stale share can't outlive the
        // promise that a private note belongs to its creator only.
        Assert.Null(await context.ResolveForAsync(recipientId, noteId));
        Assert.NotNull(await context.ResolveForAsync(context.OwnerId, noteId));
    }

    [Fact]
    public async Task A_private_note_never_appears_in_someone_elses_list()
    {
        var context = new PrivateNoteTestContext();
        var recipientId = Guid.NewGuid();
        var noteId = await context.CreateAsync("Shopping", [], isPrivate: false, encryptedContent: null);
        await context.ShareAndAcceptAsync(noteId, recipientId);

        await context.UpdateAsync(noteId, "Shopping", [], isPrivate: true, SealedContent);

        Assert.Empty(await context.ResolveAllForAsync(recipientId));
        Assert.Single(await context.ResolveAllForAsync(context.OwnerId));
    }

    /// <summary>The collaborator graph these flows need, wired the way DI wires the real one.</summary>
    private sealed class PrivateNoteTestContext
    {
        public InMemoryNoteRepository NoteRepository { get; } = new();
        public InMemoryNoteShareRepository NoteShareRepository { get; } = new();
        public InMemoryUserRepository UserRepository { get; } = new();
        public Guid OwnerId { get; } = Guid.NewGuid();

        private NoteAccessResolver Resolver => new(NoteRepository, NoteShareRepository, UserRepository);

        public Task<Guid> CreateAsync(string title, IReadOnlyList<NoteContentLine> content, bool isPrivate, EncryptedPayload? encryptedContent)
            => new CreateNoteCommandHandler(NoteRepository)
                .HandleAsync(new CreateNoteCommand(OwnerId, title, content, isPrivate, encryptedContent), CancellationToken.None);

        public Task<EditOutcome> UpdateAsync(
            Guid noteId, string title, IReadOnlyList<NoteContentLine> content, bool isPrivate, EncryptedPayload? encryptedContent)
            => new UpdateNoteCommandHandler(Resolver, NoteRepository)
                .HandleAsync(new UpdateNoteCommand(OwnerId, noteId, title, content, isPrivate, encryptedContent), CancellationToken.None);

        public Task<ShareOutcome?> ShareAsync(Guid noteId, Guid recipientId)
            => new ShareNoteCommandHandler(Resolver, NoteShareRepository)
                .HandleAsync(new ShareNoteCommand(OwnerId, noteId, recipientId, ShareAccessLevel.ReadOnly), CancellationToken.None);

        public async Task ShareAndAcceptAsync(Guid noteId, Guid recipientId)
        {
            var outcome = await ShareAsync(noteId, recipientId);
            var share = await NoteShareRepository.GetByIdAsync(recipientId, outcome!.ShareId, CancellationToken.None);
            share!.MarkAccepted();
            await NoteShareRepository.UpdateAsync(share, CancellationToken.None);
        }

        public Task<Note?> ResolveForAsync(Guid callerId, Guid noteId) => Resolver.ResolveAsync(callerId, noteId, CancellationToken.None);

        public Task<IReadOnlyList<Note>> ResolveAllForAsync(Guid callerId) => Resolver.ResolveAllAsync(callerId, CancellationToken.None);
    }
}
