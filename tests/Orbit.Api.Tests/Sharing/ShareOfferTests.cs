using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Calendar;
using Orbit.Core.Inventories;
using Orbit.Core.Notes;
using Orbit.Core.Notifications;
using Orbit.Core.Sharing;
using Orbit.Core.Sharing.GetShareOffer;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Sharing;

/// <summary>
/// What the page a share's notification leads to shows: who was offered what, and whether they have
/// taken it up. Every read is scoped to the reader, so an offer made to somebody else reads exactly
/// like one that was withdrawn - which is the point, since answering differently would say whether a
/// share id exists.
/// </summary>
public sealed class ShareOfferTests
{
    private static readonly Guid OwnerUserId = Guid.NewGuid();
    private static readonly Guid RecipientUserId = Guid.NewGuid();

    private readonly InMemoryNoteRepository _notes = new();
    private readonly InMemoryNoteShareRepository _noteShares = new();
    private readonly InMemoryTaskRepository _taskLists = new();
    private readonly InMemoryTaskListShareRepository _taskListShares = new();
    private readonly InMemoryCalendarEventRepository _events = new();
    private readonly InMemoryCalendarEventShareRepository _eventShares = new();
    private readonly InMemoryInventoryRepository _inventories = new();
    private readonly InMemoryInventoryShareRepository _inventoryShares = new();

    private GetShareOfferQueryHandler Handler => new(
        _noteShares, _taskListShares, _eventShares, _inventoryShares,
        new SharedItemName(_notes, _taskLists, _events, _inventories));

    [Fact]
    public async Task An_offered_note_is_named_and_says_it_is_not_taken_up_yet()
    {
        var note = Note.Create(OwnerUserId, "Shopping", [NoteContentLine.PlainText("Milk")]);
        await _notes.AddAsync(note, CancellationToken.None);
        var share = NoteShare.Create(note.Id, OwnerUserId, RecipientUserId);
        await _noteShares.AddAsync(share, CancellationToken.None);

        var offer = await Handler.HandleAsync(
            new GetShareOfferQuery(RecipientUserId, SharedItemKind.Note, share.Id), CancellationToken.None);

        Assert.NotNull(offer);
        Assert.Equal(note.Id, offer!.ItemId);
        Assert.Equal("Shopping", offer.ItemTitle);
        Assert.False(offer.IsAccepted);
    }

    [Fact]
    public async Task An_offer_already_taken_up_says_so()
    {
        var note = Note.Create(OwnerUserId, "Shopping", [NoteContentLine.PlainText("Milk")]);
        await _notes.AddAsync(note, CancellationToken.None);
        var share = NoteShare.Create(note.Id, OwnerUserId, RecipientUserId);
        share.MarkAccepted();
        await _noteShares.AddAsync(share, CancellationToken.None);

        var offer = await Handler.HandleAsync(
            new GetShareOfferQuery(RecipientUserId, SharedItemKind.Note, share.Id), CancellationToken.None);

        Assert.True(offer!.IsAccepted);
    }

    /// <summary>
    /// Somebody else's offer is not there to read. The same answer a withdrawn one gets, deliberately.
    /// </summary>
    [Fact]
    public async Task An_offer_made_to_somebody_else_is_not_there()
    {
        var note = Note.Create(OwnerUserId, "Shopping", [NoteContentLine.PlainText("Milk")]);
        await _notes.AddAsync(note, CancellationToken.None);
        var share = NoteShare.Create(note.Id, OwnerUserId, Guid.NewGuid());
        await _noteShares.AddAsync(share, CancellationToken.None);

        var offer = await Handler.HandleAsync(
            new GetShareOfferQuery(RecipientUserId, SharedItemKind.Note, share.Id), CancellationToken.None);

        Assert.Null(offer);
    }

    [Fact]
    public async Task An_offer_that_never_existed_is_not_there_either()
        => Assert.Null(await Handler.HandleAsync(
            new GetShareOfferQuery(RecipientUserId, SharedItemKind.Note, Guid.NewGuid()), CancellationToken.None));

    /// <summary>
    /// Deleted between the offer and the reading of it: the offer still stands and can still be taken
    /// up - the name is the only thing lost, and it comes back empty rather than taking the offer with
    /// it.
    /// </summary>
    [Fact]
    public async Task An_offer_whose_thing_is_gone_still_stands_and_has_no_name()
    {
        var share = NoteShare.Create(Guid.NewGuid(), OwnerUserId, RecipientUserId);
        await _noteShares.AddAsync(share, CancellationToken.None);

        var offer = await Handler.HandleAsync(
            new GetShareOfferQuery(RecipientUserId, SharedItemKind.Note, share.Id), CancellationToken.None);

        Assert.NotNull(offer);
        Assert.Equal(string.Empty, offer!.ItemTitle);
    }

    [Fact]
    public async Task A_task_list_is_read_the_same_way()
    {
        var taskList = TaskList.Create(OwnerUserId, "Moving", []);
        await _taskLists.AddAsync(taskList, CancellationToken.None);
        var share = TaskListShare.Create(taskList.Id, OwnerUserId, RecipientUserId);
        await _taskListShares.AddAsync(share, CancellationToken.None);

        var offer = await Handler.HandleAsync(
            new GetShareOfferQuery(RecipientUserId, SharedItemKind.TaskList, share.Id), CancellationToken.None);

        Assert.Equal("Moving", offer!.ItemTitle);
        Assert.Equal(taskList.Id, offer.ItemId);
    }

    [Fact]
    public async Task An_event_is_read_the_same_way()
    {
        var calendarEvent = CalendarEvent.Create(
            OwnerUserId,
            new CalendarEventDetails(
                "Dentist", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), false,
                null, [], [], ReminderNotificationChannel: NotificationChannel.None));
        await _events.AddAsync(calendarEvent, CancellationToken.None);
        var share = CalendarEventShare.Create(calendarEvent.Id, OwnerUserId, RecipientUserId);
        await _eventShares.AddAsync(share, CancellationToken.None);

        var offer = await Handler.HandleAsync(
            new GetShareOfferQuery(RecipientUserId, SharedItemKind.CalendarEvent, share.Id), CancellationToken.None);

        Assert.Equal("Dentist", offer!.ItemTitle);
    }

    [Fact]
    public async Task An_inventory_is_read_the_same_way()
    {
        var inventory = Inventory.Create(OwnerUserId, "Pantry");
        await _inventories.AddAsync(inventory, CancellationToken.None);
        var share = InventoryShare.Create(inventory.Id, OwnerUserId, RecipientUserId);
        await _inventoryShares.AddAsync(share, CancellationToken.None);

        var offer = await Handler.HandleAsync(
            new GetShareOfferQuery(RecipientUserId, SharedItemKind.Inventory, share.Id), CancellationToken.None);

        Assert.Equal("Pantry", offer!.ItemTitle);
    }

    /// <summary>A position is not offered and has nothing to accept - see SharedItemLink.TheMap.</summary>
    [Fact]
    public async Task A_position_has_no_offer_to_read()
        => Assert.Null(await Handler.HandleAsync(
            new GetShareOfferQuery(RecipientUserId, SharedItemKind.Location, Guid.NewGuid()), CancellationToken.None));
}
