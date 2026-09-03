using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Core.Sharing;
using Orbit.Core.Sharing.ClaimPublicShareLink;
using Orbit.Core.Sharing.CreatePublicShareLink;
using Orbit.Core.Sharing.GetPublicSharedItem;
using Orbit.Core.Sharing.RevokePublicShareLink;
using Orbit.Core.Tasks;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Sharing;

/// <summary>
/// Covers what a public link promises and what it must refuse. The token is the entire access check, so
/// most of this is about the ways a link has to stop working - revoked, deleted, made private - and
/// about it never being a route to more than reading.
/// </summary>
public sealed class PublicShareLinkTests
{
    [Fact]
    public async Task A_link_shows_the_item_to_someone_with_no_account()
    {
        var context = new PublicShareTestContext();
        var noteId = await context.AddNoteAsync("Shopping list", "Milk", "Bread");

        var link = await context.CreateLinkAsync(SharedItemType.Note, noteId);

        var item = await context.ReadAsync(link!.Token);
        Assert.NotNull(item);
        Assert.Equal("Shopping list", item!.Title);
        Assert.Equal(["Milk", "Bread"], item.Lines.Select(line => line.Text));
        Assert.Equal("Anna Kowalska", item.OwnerDisplayName);
    }

    [Fact]
    public async Task Asking_twice_hands_back_the_same_link()
    {
        var context = new PublicShareTestContext();
        var noteId = await context.AddNoteAsync("Shopping list");

        var first = await context.CreateLinkAsync(SharedItemType.Note, noteId);
        var second = await context.CreateLinkAsync(SharedItemType.Note, noteId);

        // Two live URLs for one item would both need revoking to take it back, and whoever copied the
        // first would never know the second existed.
        Assert.Equal(first!.Token, second!.Token);
    }

    [Fact]
    public async Task Two_links_never_share_a_token()
    {
        var context = new PublicShareTestContext();

        var first = await context.CreateLinkAsync(SharedItemType.Note, await context.AddNoteAsync("One"));
        var second = await context.CreateLinkAsync(SharedItemType.Note, await context.AddNoteAsync("Two"));

        Assert.NotEqual(first!.Token, second!.Token);
        Assert.True(first.Token.Length >= 40, "A token this short would be worth guessing at.");
    }

    [Fact]
    public async Task A_revoked_link_stops_showing_anything()
    {
        var context = new PublicShareTestContext();
        var noteId = await context.AddNoteAsync("Shopping list");
        var link = await context.CreateLinkAsync(SharedItemType.Note, noteId);

        await context.RevokeLinkAsync(SharedItemType.Note, noteId);

        Assert.Null(await context.ReadAsync(link!.Token));
    }

    [Fact]
    public async Task An_unknown_token_shows_nothing_either()
    {
        var context = new PublicShareTestContext();

        // Both null, deliberately: saying which would tell someone guessing that they had guessed one.
        Assert.Null(await context.ReadAsync("not-a-real-token"));
    }

    [Fact]
    public async Task A_private_note_cannot_be_published()
    {
        var context = new PublicShareTestContext();
        var noteId = await context.AddPrivateNoteAsync();

        // Its title and content are sealed with a key only the owner's browser holds, so a reader would
        // be handed ciphertext - and offering to publish it is the wrong offer to make at all.
        Assert.Null(await context.CreateLinkAsync(SharedItemType.Note, noteId));
    }

    [Fact]
    public async Task Making_a_published_note_private_closes_its_link()
    {
        var context = new PublicShareTestContext();
        var noteId = await context.AddNoteAsync("Shopping list");
        var link = await context.CreateLinkAsync(SharedItemType.Note, noteId);

        await context.MakeNotePrivateAsync(noteId);

        // Refusing new links would not be enough - the one already handed out has to close too.
        Assert.Null(await context.ReadAsync(link!.Token));
    }

    [Fact]
    public async Task Deleting_the_item_closes_its_link()
    {
        var context = new PublicShareTestContext();
        var noteId = await context.AddNoteAsync("Shopping list");
        var link = await context.CreateLinkAsync(SharedItemType.Note, noteId);

        await context.DeleteNoteAsync(noteId);

        Assert.Null(await context.ReadAsync(link!.Token));
    }

    [Fact]
    public async Task Someone_who_does_not_own_the_item_cannot_publish_it()
    {
        var context = new PublicShareTestContext();
        var noteId = await context.AddNoteAsync("Shopping list");

        // A link they made would outlive whatever access they had, and would not be the owner's to revoke.
        Assert.Null(await context.CreateLinkAsync(SharedItemType.Note, noteId, asUserId: context.ReaderId));
    }

    [Fact]
    public async Task Claiming_a_link_puts_the_item_in_your_own_account()
    {
        var context = new PublicShareTestContext();
        var noteId = await context.AddNoteAsync("Shopping list");
        var link = await context.CreateLinkAsync(SharedItemType.Note, noteId);

        var result = await context.ClaimAsync(link!.Token, context.ReaderId);

        Assert.True(result.Claimed);
        var share = await context.NoteShareRepository.FindAcceptedGrantAsync(noteId, context.ReaderId, CancellationToken.None);
        Assert.NotNull(share);
        Assert.Equal(ShareAccessLevel.ReadOnly, share!.AccessLevel);
    }

    [Fact]
    public async Task A_claimed_share_needs_no_accepting()
    {
        var context = new PublicShareTestContext();
        var link = await context.CreateLinkAsync(SharedItemType.Note, await context.AddNoteAsync("Shopping list"));

        await context.ClaimAsync(link!.Token, context.ReaderId);

        // Unlike a share the owner offers by name: whoever claimed it asked for it themselves, so there
        // is nothing left to agree to.
        var share = Assert.Single(
            await context.NoteShareRepository.GetAcceptedGrantsForRecipientAsync(context.ReaderId, CancellationToken.None));
        Assert.True(share.IsAccepted);
    }

    [Fact]
    public async Task A_link_never_grants_more_than_reading()
    {
        var context = new PublicShareTestContext();
        var link = await context.CreateLinkAsync(SharedItemType.Note, await context.AddNoteAsync("Shopping list"));

        await context.ClaimAsync(link!.Token, context.ReaderId);

        // A link is handed around and can reach anyone, so it must never be a route to editing.
        var share = Assert.Single(
            await context.NoteShareRepository.GetAcceptedGrantsForRecipientAsync(context.ReaderId, CancellationToken.None));
        Assert.Equal(ShareAccessLevel.ReadOnly, share.AccessLevel);
    }

    [Fact]
    public async Task Claiming_the_same_link_twice_grants_nothing_new()
    {
        var context = new PublicShareTestContext();
        var link = await context.CreateLinkAsync(SharedItemType.Note, await context.AddNoteAsync("Shopping list"));
        await context.ClaimAsync(link!.Token, context.ReaderId);

        var result = await context.ClaimAsync(link.Token, context.ReaderId);

        Assert.True(result.AlreadyHeld);
        Assert.Single(await context.NoteShareRepository.GetAcceptedGrantsForRecipientAsync(context.ReaderId, CancellationToken.None));
    }

    [Fact]
    public async Task Claiming_your_own_link_does_not_share_it_with_yourself()
    {
        var context = new PublicShareTestContext();
        var link = await context.CreateLinkAsync(SharedItemType.Note, await context.AddNoteAsync("Shopping list"));

        var result = await context.ClaimAsync(link!.Token, context.OwnerId);

        Assert.True(result.AlreadyHeld);
        Assert.Empty(await context.NoteShareRepository.GetAcceptedGrantsForRecipientAsync(context.OwnerId, CancellationToken.None));
    }

    [Fact]
    public async Task Claiming_a_revoked_link_is_refused()
    {
        var context = new PublicShareTestContext();
        var noteId = await context.AddNoteAsync("Shopping list");
        var link = await context.CreateLinkAsync(SharedItemType.Note, noteId);
        await context.RevokeLinkAsync(SharedItemType.Note, noteId);

        var result = await context.ClaimAsync(link!.Token, context.ReaderId);

        Assert.False(result.Claimed);
        Assert.Empty(await context.NoteShareRepository.GetAcceptedGrantsForRecipientAsync(context.ReaderId, CancellationToken.None));
    }

    [Fact]
    public async Task Claiming_tells_the_claimer_what_they_now_have()
    {
        var context = new PublicShareTestContext();
        var link = await context.CreateLinkAsync(SharedItemType.Note, await context.AddNoteAsync("Shopping list"));

        await context.ClaimAsync(link!.Token, context.ReaderId);

        // The same invitation any other share leaves, so the item doesn't just silently appear.
        var announcement = Assert.Single(context.SharedItemNotifier.Announced);
        Assert.Equal(context.ReaderId, announcement.RecipientUserId);
        Assert.Equal("Shopping list", announcement.ItemTitle);
    }

    private sealed class PublicShareTestContext
    {
        private readonly InMemoryNoteRepository _noteRepository = new();
        private readonly InMemoryPublicShareLinkRepository _linkRepository = new();
        private readonly PublicSharedItemReader _reader;

        public InMemoryNoteShareRepository NoteShareRepository { get; } = new();
        public InMemoryTaskRepository TaskRepository { get; } = new();
        public InMemoryInventoryRepository InventoryRepository { get; } = new();
        public InMemoryTaskListShareRepository TaskListShareRepository { get; } = new();
        public InMemoryInventoryShareRepository InventoryShareRepository { get; } = new();
        public RecordingSharedItemNotifier SharedItemNotifier { get; } = new();
        public Guid OwnerId { get; }
        public Guid ReaderId { get; } = Guid.NewGuid();

        public PublicShareTestContext()
        {
            var userRepository = new InMemoryUserRepository();
            var owner = User.Create("anna@example.com", "anna", "Anna Kowalska", "hash");
            OwnerId = owner.Id;
            userRepository.AddAsync(owner, CancellationToken.None).GetAwaiter().GetResult();

            _reader = new PublicSharedItemReader(
                _noteRepository, TaskRepository, new InMemoryCalendarEventRepository(),
                InventoryRepository, new InMemoryInventoryItemRepository(), userRepository);
        }

        public async Task<Guid> AddNoteAsync(string title, params string[] lines)
        {
            var note = Note.Create(OwnerId, title, lines.Select(NoteContentLine.PlainText).ToList());
            await _noteRepository.AddAsync(note, CancellationToken.None);
            return note.Id;
        }

        public async Task<Guid> AddPrivateNoteAsync()
        {
            var note = Note.Create(OwnerId, string.Empty, [], isPrivate: true, new EncryptedPayload("c2VhbGVk", "bm9uY2U="));
            await _noteRepository.AddAsync(note, CancellationToken.None);
            return note.Id;
        }

        public async Task MakeNotePrivateAsync(Guid noteId)
        {
            var note = await _noteRepository.GetByIdAsync(OwnerId, noteId, CancellationToken.None);
            note!.Update(string.Empty, [], isPrivate: true, new EncryptedPayload("c2VhbGVk", "bm9uY2U="), note.Priority);
            await _noteRepository.UpdateAsync(note, CancellationToken.None);
        }

        public Task DeleteNoteAsync(Guid noteId) => _noteRepository.DeleteAsync(OwnerId, noteId, CancellationToken.None);

        public Task<PublicShareLink?> CreateLinkAsync(SharedItemType itemType, Guid itemId, Guid? asUserId = null)
            => new CreatePublicShareLinkCommandHandler(_linkRepository, _reader)
                .HandleAsync(new CreatePublicShareLinkCommand(asUserId ?? OwnerId, itemType, itemId), CancellationToken.None);

        public Task<bool> RevokeLinkAsync(SharedItemType itemType, Guid itemId)
            => new RevokePublicShareLinkCommandHandler(_linkRepository)
                .HandleAsync(new RevokePublicShareLinkCommand(OwnerId, itemType, itemId), CancellationToken.None);

        public Task<PublicSharedItem?> ReadAsync(string token)
            => new GetPublicSharedItemQueryHandler(_linkRepository, _reader)
                .HandleAsync(new GetPublicSharedItemQuery(token), CancellationToken.None);

        public Task<ClaimPublicShareLinkResult> ClaimAsync(string token, Guid claimingUserId)
            => new ClaimPublicShareLinkCommandHandler(
                    _linkRepository, _reader, NoteShareRepository, TaskListShareRepository,
                    new InMemoryCalendarEventShareRepository(), InventoryShareRepository,
                    new TaskListShareCascade(
                        TaskRepository, InventoryRepository, TaskListShareRepository, InventoryShareRepository),
                    SharedItemNotifier)
                .HandleAsync(new ClaimPublicShareLinkCommand(token, claimingUserId), CancellationToken.None);
    }
}
