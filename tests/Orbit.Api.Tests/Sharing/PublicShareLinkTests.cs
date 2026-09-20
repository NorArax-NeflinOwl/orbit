using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Folders;
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

    /// <summary>
    /// A place is the shortest thing a link can point at: what it is called, where it is, and whatever
    /// was written about it. The point itself is deliberately absent - a link is read by anybody who
    /// has it, and coordinates are the one thing on a place worth being careful with.
    /// </summary>
    [Fact]
    public async Task A_link_to_a_place_shows_its_address_and_not_its_point()
    {
        var context = new PublicShareTestContext();
        var placeId = await context.AddPlaceAsync("The good bakery", "Sourdough on Thursdays");

        var link = await context.CreateLinkAsync(SharedItemType.Place, placeId);

        var item = await context.ReadAsync(link!.Token);
        Assert.NotNull(item);
        Assert.Equal("The good bakery", item!.Title);
        Assert.Equal("Rynek 1, Lublin", item.Subtitle);
        Assert.Equal(["Sourdough on Thursdays"], item.Lines.Select(line => line.Text));
        Assert.DoesNotContain("51.2465", string.Join(" ", item.Lines.Select(line => line.Text)));
    }

    /// <summary>
    /// And somebody signed in can keep what the link showed them, the way they can with the other
    /// kinds - the share it makes is ReadOnly and accepted on the spot.
    /// </summary>
    [Fact]
    public async Task A_place_behind_a_link_can_be_kept_by_whoever_opened_it()
    {
        var context = new PublicShareTestContext();
        var placeId = await context.AddPlaceAsync("The good bakery");
        var link = await context.CreateLinkAsync(SharedItemType.Place, placeId);

        var claim = await context.ClaimAsync(link!.Token, context.ReaderId);

        Assert.True(claim.Claimed);
        var grant = await context.PlaceShareRepository.FindAcceptedGrantAsync(
            placeId, context.ReaderId, CancellationToken.None);
        Assert.NotNull(grant);
        Assert.Equal(ShareAccessLevel.ReadOnly, grant!.AccessLevel);
    }

    /// <summary>
    /// A link can point at a whole folder since 2026-09-20, which the user asked for: one address
    /// showing everything filed under a tab, each thing drawn the way its own link would draw it.
    /// </summary>
    [Fact]
    public async Task A_link_to_a_folder_shows_everything_filed_under_it()
    {
        var context = new PublicShareTestContext();
        var folderId = await context.AddNotesFolderAsync("Recipes");
        await context.FileNoteAsync(await context.AddNoteAsync("Pierogi", "Flour", "Potato"), folderId);
        await context.FileNoteAsync(await context.AddNoteAsync("Bigos", "Cabbage"), folderId);
        await context.AddNoteAsync("Shopping list");

        var link = await context.CreateLinkAsync(SharedItemType.Folder, folderId);

        var item = await context.ReadAsync(link!.Token);
        Assert.NotNull(item);
        Assert.Equal("Recipes", item!.Title);
        Assert.Equal("2 items", item.Subtitle);
        Assert.Equal(["Pierogi", "Bigos"], item.AllItems.Select(inside => inside.Title));
        // Each one whole, rather than a list of names: what a folder's link is for is reading them.
        Assert.Equal(["Flour", "Potato"], item.AllItems[0].Lines.Select(line => line.Text));
        // And nothing that is not in the folder.
        Assert.DoesNotContain(item.AllItems, inside => inside.Title == "Shopping list");
    }

    /// <summary>
    /// What is sealed stays sealed, and what has been put away stays away. A link is read by anybody
    /// who has it, and the archive is not what the owner meant to hand over.
    /// </summary>
    [Fact]
    public async Task A_folders_link_leaves_out_what_is_sealed_and_what_is_put_away()
    {
        var context = new PublicShareTestContext();
        var folderId = await context.AddNotesFolderAsync("Recipes");
        await context.FileNoteAsync(await context.AddNoteAsync("Pierogi"), folderId);
        await context.FileNoteAsync(await context.AddPrivateNoteAsync(), folderId);
        var lastYear = await context.AddNoteAsync("Last year");
        await context.FileNoteAsync(lastYear, folderId);
        await context.ArchiveNoteAsync(lastYear);

        var link = await context.CreateLinkAsync(SharedItemType.Folder, folderId);

        var item = await context.ReadAsync(link!.Token);
        Assert.Equal(["Pierogi"], item!.AllItems.Select(inside => inside.Title));
    }

    /// <summary>An emptied folder still opens and says so, rather than reading as a link somebody revoked.</summary>
    [Fact]
    public async Task A_folder_with_nothing_in_it_still_opens()
    {
        var context = new PublicShareTestContext();
        var folderId = await context.AddNotesFolderAsync("Recipes");

        var link = await context.CreateLinkAsync(SharedItemType.Folder, folderId);

        var item = await context.ReadAsync(link!.Token);
        Assert.NotNull(item);
        Assert.Empty(item!.AllItems);
    }

    [Fact]
    public async Task Somebody_elses_folder_cannot_be_published()
    {
        var context = new PublicShareTestContext();
        var folderId = await context.AddNotesFolderAsync("Recipes");

        Assert.Null(await context.CreateLinkAsync(SharedItemType.Folder, folderId, asUserId: context.ReaderId));
    }

    /// <summary>
    /// And a folder's link is for reading. There is no such thing as a share of a folder - it is the
    /// owner's own tab - and the button's promise is one read-only copy, where a folder would hand over
    /// a page of them, unfiled. Being given the folder in Orbit is the other half of sharing one.
    /// </summary>
    [Fact]
    public async Task A_folders_link_cannot_be_claimed()
    {
        var context = new PublicShareTestContext();
        var folderId = await context.AddNotesFolderAsync("Recipes");
        await context.FileNoteAsync(await context.AddNoteAsync("Pierogi"), folderId);
        var link = await context.CreateLinkAsync(SharedItemType.Folder, folderId);

        var result = await context.ClaimAsync(link!.Token, context.ReaderId);

        Assert.False(result.Claimed);
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
        public InMemoryPlaceShareRepository PlaceShareRepository { get; } = new();
        public InMemoryPlaceRepository PlaceRepository { get; } = new();
        public InMemoryFolderRepository Folders { get; } = new();
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
                InventoryRepository, new InMemoryInventoryItemRepository(), PlaceRepository,
                userRepository, Folders);
        }

        public async Task<Guid> AddNoteAsync(string title, params string[] lines)
        {
            var note = Note.Create(OwnerId, title, lines.Select(NoteContentLine.PlainText).ToList());
            await _noteRepository.AddAsync(note, CancellationToken.None);
            return note.Id;
        }

        /// <summary>Somewhere kept on the map - the shortest thing a link can point at.</summary>
        public async Task<Guid> AddPlaceAsync(string name, string description = "", string address = "Rynek 1, Lublin")
        {
            var place = Orbit.Core.Places.Place.Create(
                OwnerId, name, description, new Orbit.Core.Calendar.EventLocation(address, 51.2465, 22.5684), isPrivate: false);
            await PlaceRepository.AddAsync(place, CancellationToken.None);
            return place.Id;
        }

        /// <summary>A folder of the owner's, on the notes - what a link to a whole tab points at.</summary>
        public async Task<Guid> AddNotesFolderAsync(string name)
        {
            var folder = Folder.Create(OwnerId, name, FolderScope.Notes);
            await Folders.AddAsync(folder, CancellationToken.None);
            return folder.Id;
        }

        /// <summary>Files an existing note under one - see Note.MoveToFolder.</summary>
        public async Task FileNoteAsync(Guid noteId, Guid folderId)
        {
            var note = await _noteRepository.GetByIdAsync(OwnerId, noteId, CancellationToken.None);
            note!.MoveToFolder(folderId);
            await _noteRepository.UpdateAsync(note, CancellationToken.None);
        }

        /// <summary>And puts one away, which takes it out of what a folder's link shows.</summary>
        public async Task ArchiveNoteAsync(Guid noteId)
        {
            var note = await _noteRepository.GetByIdAsync(OwnerId, noteId, CancellationToken.None);
            note!.Archive(true);
            await _noteRepository.UpdateAsync(note, CancellationToken.None);
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
                    new InMemoryCalendarEventShareRepository(), InventoryShareRepository, PlaceShareRepository,
                    new TaskListShareCascade(
                        TaskRepository, InventoryRepository, TaskListShareRepository, InventoryShareRepository),
                    SharedItemNotifier)
                .HandleAsync(new ClaimPublicShareLinkCommand(token, claimingUserId), CancellationToken.None);
    }
}
