using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Chat;
using Orbit.Core.Inventories;
using Orbit.Core.Notes;
using Orbit.Core.Notifications;
using Orbit.Core.Places;
using Orbit.Core.Sharing;
using Orbit.Core.Sharing.GetSharesWith;
using Orbit.Core.Sharing.RevokeShare;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Sharing;

/// <summary>
/// "What have I given this person, and can I take it back" - the question the contact's own card asks.
/// Until this existed the only way to answer it was to open every note, list, event and shelf in turn
/// and read its sharing panel, and there was no way at all to withdraw one from the owner's end.
///
/// The four kinds meet in one query and one command, so what these mostly pin is that all four are
/// actually reached and that both are scoped to the owner.
/// </summary>
public sealed class WhatSomebodyHasBeenGivenTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid RecipientId = Guid.NewGuid();

    [Fact]
    public async Task Everything_given_to_one_person_is_listed_whatever_kind_it_is()
    {
        var context = new SharingTestContext();
        await context.ShareANoteAsync();
        await context.ShareATaskListAsync();
        await context.ShareAnEventAsync();
        await context.ShareAnInventoryAsync();

        var shared = await context.ListAsync();

        Assert.Equal(
            [SharedItemKind.Note, SharedItemKind.TaskList, SharedItemKind.CalendarEvent, SharedItemKind.Inventory],
            shared.Select(share => share.Kind).OrderBy(kind => kind));
    }

    /// <summary>
    /// An offer nobody has taken up is on the list too, and says so. It is still access somebody has
    /// been given, and withdrawing it before it is accepted is the likeliest reason to be reading this.
    /// </summary>
    [Fact]
    public async Task An_offer_nobody_accepted_is_listed_and_says_so()
    {
        var context = new SharingTestContext();
        await context.ShareANoteAsync(accepted: false);

        var share = Assert.Single(await context.ListAsync());

        Assert.False(share.IsAccepted);
    }

    /// <summary>Only what this owner gave: somebody else's share of the same note is not theirs to see.</summary>
    [Fact]
    public async Task Somebody_elses_share_of_the_same_thing_is_not_listed()
    {
        var context = new SharingTestContext();
        await context.ShareANoteAsync();
        await context.NoteShares.AddAsync(
            NoteShare.Create(Guid.NewGuid(), Guid.NewGuid(), RecipientId), CancellationToken.None);

        Assert.Single(await context.ListAsync());
    }

    /// <summary>And only what this person was given: a note handed to a third party is a different row.</summary>
    [Fact]
    public async Task What_was_given_to_somebody_else_is_not_listed()
    {
        var context = new SharingTestContext();
        await context.ShareANoteAsync();
        await context.NoteShares.AddAsync(
            NoteShare.Create(Guid.NewGuid(), OwnerId, Guid.NewGuid()), CancellationToken.None);

        Assert.Single(await context.ListAsync());
    }

    [Fact]
    public async Task Taking_one_back_removes_it()
    {
        var context = new SharingTestContext();
        var shareId = await context.ShareANoteAsync();

        Assert.True(await context.RevokeAsync(SharedItemKind.Note, shareId));

        Assert.Empty(await context.ListAsync());
    }

    /// <summary>
    /// Scoped to the owner: a share somebody else made is not this caller's to withdraw, and answers
    /// exactly as one that has already gone. Telling those apart would say whether a share id exists.
    /// </summary>
    [Fact]
    public async Task A_share_that_is_not_the_callers_is_not_theirs_to_take_back()
    {
        var context = new SharingTestContext();
        var somebodyElses = NoteShare.Create(Guid.NewGuid(), Guid.NewGuid(), RecipientId);
        await context.NoteShares.AddAsync(somebodyElses, CancellationToken.None);

        Assert.False(await context.RevokeAsync(SharedItemKind.Note, somebodyElses.Id));
        // And it is still there: a refusal must not be a deletion that reported itself badly. Read
        // through the repository's own scoped lookup, which is the only way in.
        Assert.NotNull(await context.NoteShares.GetByIdAsync(RecipientId, somebodyElses.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Taking_back_something_already_gone_says_so_rather_than_throwing()
    {
        var context = new SharingTestContext();

        Assert.False(await context.RevokeAsync(SharedItemKind.Note, Guid.NewGuid()));
    }

    /// <summary>
    /// A position is not a share row at all - it lives in its own table and is withdrawn from the map.
    /// Named in the handler rather than swept into a default, and answered as "nothing here" instead of
    /// reaching one of the four repositories with an id that does not belong to it.
    /// </summary>
    [Fact]
    public async Task A_shared_position_is_not_withdrawn_through_here()
    {
        var context = new SharingTestContext();

        Assert.False(await context.RevokeAsync(SharedItemKind.Location, Guid.NewGuid()));
    }

    /// <summary>
    /// The invitation goes with the access. What the recipient pressed "Accept" on is a chat message,
    /// and leaving it behind leaves an offer in the conversation that now leads nowhere.
    /// </summary>
    [Fact]
    public async Task Taking_one_back_takes_its_invitation_out_of_the_conversation()
    {
        var context = new SharingTestContext();
        var shareId = await context.ShareANoteAsync();
        var invitation = await context.AnnounceTheShareInChatAsync(shareId);

        await context.RevokeAsync(SharedItemKind.Note, shareId);

        Assert.True(invitation.IsDeleted);
        Assert.Equal(OwnerId, invitation.DeletedByUserId);
        // And the words are actually gone, not only marked - see ChatMessage.Delete.
        Assert.Equal(string.Empty, invitation.CiphertextBase64);
    }

    /// <summary>
    /// Somebody else's message, sent in the same conversation, is not swept up with it. Only the one
    /// that named this share goes.
    /// </summary>
    [Fact]
    public async Task An_ordinary_message_in_the_same_conversation_is_left_alone()
    {
        var context = new SharingTestContext();
        var shareId = await context.ShareANoteAsync();
        var chatter = await context.SaySomethingInChatAsync();

        await context.RevokeAsync(SharedItemKind.Note, shareId);

        Assert.False(chatter.IsDeleted);
    }

    /// <summary>
    /// The recipient is told, or the withdrawn invitation sits on their screen still offering an
    /// "Accept" until the slow poll comes round.
    /// </summary>
    [Fact]
    public async Task Both_ends_of_the_conversation_hear_about_it()
    {
        var context = new SharingTestContext();
        var shareId = await context.ShareANoteAsync();
        await context.AnnounceTheShareInChatAsync(shareId);

        await context.RevokeAsync(SharedItemKind.Note, shareId);

        // As a set: two ids made by Guid.NewGuid() sort into whichever order they happen to sort into,
        // and which of the two was told first is not a fact about this.
        Assert.Equal<IReadOnlySet<Guid>>(
            new HashSet<Guid> { OwnerId, RecipientId }, context.LiveUpdates.ChatToldAbout.ToHashSet());
    }

    /// <summary>
    /// A share offered before invitations recorded which share they announced has no message to match,
    /// and one offered from a client that does not say so has none either. The access still goes, which
    /// is what was asked for, and nobody is told about a chat that did not change.
    /// </summary>
    [Fact]
    public async Task A_share_whose_invitation_cannot_be_found_is_still_withdrawn()
    {
        var context = new SharingTestContext();
        var shareId = await context.ShareANoteAsync();

        Assert.True(await context.RevokeAsync(SharedItemKind.Note, shareId));

        Assert.Empty(context.LiveUpdates.ChatToldAbout);
    }

    private sealed class SharingTestContext
    {
        public InMemoryNoteShareRepository NoteShares { get; } = new();
        public InMemoryChatMessageRepository Messages { get; } = new();
        public RecordingLiveUpdatePublisher LiveUpdates { get; } = new();
        private readonly InMemoryTaskListShareRepository _taskListShares = new();
        private readonly InMemoryCalendarEventShareRepository _calendarEventShares = new();
        private readonly InMemoryInventoryShareRepository _inventoryShares = new();
        private readonly InMemoryNoteRepository _notes = new();
        private readonly InMemoryTaskRepository _taskLists = new();
        private readonly InMemoryCalendarEventRepository _events = new();
        private readonly InMemoryInventoryRepository _inventories = new();

        public async Task<Guid> ShareANoteAsync(bool accepted = true)
        {
            var note = Note.Create(OwnerId, "Shopping list", [NoteContentLine.PlainText("Milk")]);
            await _notes.AddAsync(note, CancellationToken.None);
            var share = NoteShare.Create(note.Id, OwnerId, RecipientId);
            if (accepted)
            {
                share.MarkAccepted();
            }

            await NoteShares.AddAsync(share, CancellationToken.None);
            return share.Id;
        }

        public async Task ShareATaskListAsync()
        {
            var taskList = TaskList.Create(OwnerId, "Errands", []);
            await _taskLists.AddAsync(taskList, CancellationToken.None);
            var share = TaskListShare.Create(taskList.Id, OwnerId, RecipientId);
            share.MarkAccepted();
            await _taskListShares.AddAsync(share, CancellationToken.None);
        }

        public async Task ShareAnEventAsync()
        {
            var share = CalendarEventShare.Create(Guid.NewGuid(), OwnerId, RecipientId);
            share.MarkAccepted();
            await _calendarEventShares.AddAsync(share, CancellationToken.None);
        }

        public async Task ShareAnInventoryAsync()
        {
            var share = InventoryShare.Create(Guid.NewGuid(), OwnerId, RecipientId);
            share.MarkAccepted();
            await _inventoryShares.AddAsync(share, CancellationToken.None);
        }

        /// <summary>The places this owner keeps, and the grants they have handed out over them.</summary>
        private readonly InMemoryPlaceRepository _places = new();
        internal InMemoryPlaceShareRepository PlaceShares { get; } = new();

        /// <summary>Hands over a place and says it was taken up, the way the others here do.</summary>
        public async Task<Guid> SharePlaceAsync(string name)
        {
            var place = Place.Create(OwnerId, name, "", new EventLocation("Piękna 1, Warszawa", 52.2297, 21.0122), isPrivate: false);
            await _places.AddAsync(place, CancellationToken.None);
            var share = PlaceShare.Create(place.Id, OwnerId, RecipientId);
            share.MarkAccepted();
            await PlaceShares.AddAsync(share, CancellationToken.None);
            return share.Id;
        }

        public Task<IReadOnlyList<SharedWithSomebody>> ListAsync()
            => new GetSharesWithQueryHandler(
                    NoteShares, _taskListShares, _calendarEventShares, _inventoryShares, PlaceShares,
                    new SharedItemName(_notes, _taskLists, _events, _inventories, _places))
                .HandleAsync(new GetSharesWithQuery(OwnerId, RecipientId), CancellationToken.None);

        /// <summary>The message the editors send right after sharing - see EncryptedChatMessageSender.</summary>
        public async Task<ChatMessage> AnnounceTheShareInChatAsync(Guid shareId)
        {
            var invitation = ChatMessage.Create(OwnerId, RecipientId, "sealed", "nonce", shareId);
            await Messages.AddAsync(invitation, CancellationToken.None);
            return invitation;
        }

        public async Task<ChatMessage> SaySomethingInChatAsync()
        {
            var message = ChatMessage.Create(OwnerId, RecipientId, "sealed", "nonce");
            await Messages.AddAsync(message, CancellationToken.None);
            return message;
        }

        public Task<bool> RevokeAsync(SharedItemKind kind, Guid shareId)
            => new RevokeShareCommandHandler(
                    NoteShares, _taskListShares, _calendarEventShares, _inventoryShares, PlaceShares,
                    Messages, LiveUpdates)
                .HandleAsync(new RevokeShareCommand(OwnerId, kind, shareId), CancellationToken.None);
    }
}
