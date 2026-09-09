using Orbit.Api.Tests.TestDoubles;
using Orbit.Core;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.DuplicateCalendarEvent;
using Orbit.Core.Inventories;
using Orbit.Core.Inventories.DuplicateInventory;
using Orbit.Core.Notes;
using Orbit.Core.Notes.DuplicateNote;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.DuplicateTaskList;
using Xunit;

namespace Orbit.Api.Tests.Sharing;

/// <summary>
/// Duplicating something: a second note, list, storage or appointment with everything that was on it.
/// One question asked of four modules, so the rules they share are worth pinning in one place - who may
/// ask (only an owner), what a copy is named, and the handful of things a copy deliberately does not
/// carry.
/// </summary>
public sealed class MakingASecondOneTests
{
    private static readonly Guid OwnerUserId = Guid.NewGuid();
    private static readonly Guid SomebodyElse = Guid.NewGuid();

    private readonly InMemoryNoteRepository _notes = new();
    private readonly InMemoryTaskRepository _taskLists = new();
    private readonly InMemoryCalendarEventRepository _calendarEvents = new();
    private readonly InventoryTestContext _inventories = new();

    [Fact]
    public async Task A_copy_of_a_note_carries_what_the_note_said_and_is_named_by_the_caller()
    {
        var note = Note.Create(OwnerUserId, "Shopping", [NoteContentLine.PlainText("Milk")]);
        await _notes.AddAsync(note, CancellationToken.None);

        var copyId = await DuplicateNoteAsync(note.Id, "Shopping (copy)");

        var copy = await _notes.GetByIdAsync(OwnerUserId, copyId!.Value, CancellationToken.None);
        Assert.NotEqual(note.Id, copy!.Id);
        Assert.Equal("Shopping (copy)", copy.Title);
        Assert.Equal("Milk", Assert.Single(copy.Content).Text);
    }

    /// <summary>A caller that says nothing about the name keeps the original's - see DuplicateRequest.Name.</summary>
    [Fact]
    public async Task A_copy_keeps_the_original_name_when_the_caller_names_nothing()
    {
        var note = Note.Create(OwnerUserId, "Shopping", [NoteContentLine.PlainText("Milk")]);
        await _notes.AddAsync(note, CancellationToken.None);

        var copyId = await DuplicateNoteAsync(note.Id, name: null);

        var copy = await _notes.GetByIdAsync(OwnerUserId, copyId!.Value, CancellationToken.None);
        Assert.Equal("Shopping", copy!.Title);
    }

    /// <summary>
    /// Somebody else's note is not this reader's to copy: a copy would put a note on their page that its
    /// author never wrote there. The repository is scoped to the owner, which is what answers null.
    /// </summary>
    [Fact]
    public async Task Somebody_elses_note_is_not_theirs_to_copy()
    {
        var note = Note.Create(SomebodyElse, "Theirs", [NoteContentLine.PlainText("Milk")]);
        await _notes.AddAsync(note, CancellationToken.None);

        Assert.Null(await DuplicateNoteAsync(note.Id, "Theirs (copy)"));
    }

    /// <summary>A sealed note is copied as it stands - the ciphertext is the note, and the copy has the same owner.</summary>
    [Fact]
    public async Task A_sealed_note_is_copied_sealed()
    {
        var payload = new EncryptedPayload("c2VhbGVk", "bm9uY2U=");
        var note = Note.Create(OwnerUserId, string.Empty, [], isPrivate: true, payload);
        await _notes.AddAsync(note, CancellationToken.None);

        var copyId = await DuplicateNoteAsync(note.Id, name: null);

        var copy = await _notes.GetByIdAsync(OwnerUserId, copyId!.Value, CancellationToken.None);
        Assert.True(copy!.IsPrivate);
        Assert.Equal(payload.Ciphertext, copy.EncryptedContent!.Ciphertext);
    }

    [Fact]
    public async Task A_copy_of_a_list_carries_its_entries_with_new_identities()
    {
        var entry = TaskItem.Create("Buy screws", null, isCompleted: true);
        var taskList = TaskList.Create(OwnerUserId, "Shed", [entry]);
        await _taskLists.AddAsync(taskList, CancellationToken.None);

        var copy = await DuplicatedTaskListAsync(taskList.Id, "Shed (copy)");

        Assert.Equal("Shed (copy)", copy.Title);
        var copiedEntry = Assert.Single(copy.Items);
        Assert.Equal("Buy screws", copiedEntry.Description);
        Assert.True(copiedEntry.IsCompleted);
        // A new entry rather than the same one twice: two lists holding one entry is the state the
        // whole aggregate is arranged to prevent.
        Assert.NotEqual(entry.Id, copiedEntry.Id);
    }

    /// <summary>
    /// An event is raised by exactly one entry (CalendarEventDestination.RaisedBy returns the first it
    /// finds), so a second entry pointing at the same one would make which of them owns it a matter of
    /// iteration order. The entry is copied as ordinary work instead.
    /// </summary>
    [Fact]
    public async Task A_copied_entry_does_not_point_at_the_originals_appointment()
    {
        var appointment = TaskItem.Create(
            "Pick up the keys", null, isCompleted: false,
            subject: new TaskItemSubject(TaskItemKind.Calendar, linkedCalendarEventId: Guid.NewGuid()));
        var taskList = TaskList.Create(OwnerUserId, "Moving", [appointment]);
        await _taskLists.AddAsync(taskList, CancellationToken.None);

        var copy = await DuplicatedTaskListAsync(taskList.Id, "Moving (copy)");

        Assert.Null(Assert.Single(copy.Items).LinkedCalendarEventId);
    }

    /// <summary>
    /// The reader has not said this list is finished; they said it about the other one. Its entries
    /// carry their own ticks, so a copy of a list whose work is all done still reads as done - that
    /// answer came from the entries and travels with them.
    /// </summary>
    [Fact]
    public async Task A_copy_does_not_carry_the_readers_own_answer_about_being_finished()
    {
        var taskList = TaskList.Create(OwnerUserId, "Shed", [TaskItem.Create("Buy screws", null, isCompleted: false)]);
        taskList.SetCompletion(TaskListCompletion.Finished);
        await _taskLists.AddAsync(taskList, CancellationToken.None);

        var copy = await DuplicatedTaskListAsync(taskList.Id, "Shed (copy)");

        Assert.Equal(TaskListCompletion.FromTheEntries, copy.Completion);
        Assert.False(copy.IsCompleted);
    }

    /// <summary>The pin says where a card sits on this reader's page, and two cards cannot both be that one.</summary>
    [Fact]
    public async Task A_copy_is_not_pinned()
    {
        var taskList = TaskList.Create(OwnerUserId, "Shed", []);
        taskList.SetPinned(true);
        await _taskLists.AddAsync(taskList, CancellationToken.None);

        var copy = await DuplicatedTaskListAsync(taskList.Id, "Shed (copy)");

        Assert.False(copy.IsPinned);
    }

    [Fact]
    public async Task A_copy_of_a_storage_carries_what_was_on_its_shelves()
    {
        var inventoryId = _inventories.AddInventory(OwnerUserId, "Kitchen");
        await _inventories.ItemsSaver.SaveAsync(
            inventoryId,
            [new InventoryItemInput(null, "Flour", "Dry goods", ["baking"], 2, 1, InventoryUnit.Kilogram, null, NotificationChannel.None)],
            CancellationToken.None);

        var copyId = await new DuplicateInventoryCommandHandler(
                _inventories.InventoryRepository, _inventories.InventoryItemRepository, _inventories.ItemsSaver)
            .HandleAsync(new DuplicateInventoryCommand(OwnerUserId, inventoryId, "Kitchen (copy)"), CancellationToken.None);

        var copy = await _inventories.InventoryRepository.GetByIdAsync(OwnerUserId, copyId!.Value, CancellationToken.None);
        Assert.Equal("Kitchen (copy)", copy!.Name);
        var shelf = await _inventories.InventoryItemRepository.GetAllAsync(copyId.Value, CancellationToken.None);
        var item = Assert.Single(shelf);
        Assert.Equal("Flour", item.Name);
        Assert.Equal(2, item.Quantity);
        // A row of its own on a shelf of its own, not the original's row read through a second name.
        Assert.Equal(copyId.Value, item.InventoryId);
    }

    /// <summary>
    /// A guest list is a set of people who were asked to something. Copying it would invite them all
    /// again to an appointment nobody has told them about, from a press that said "duplicate".
    /// </summary>
    [Fact]
    public async Task A_copy_of_an_appointment_invites_nobody()
    {
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var calendarEvent = CalendarEvent.Create(OwnerUserId, new CalendarEventDetails(
            "Dentist", "Bring the card", Location: null, Color: "#ff0000", start, start.AddHours(1),
            IsAllDay: false, Recurrence: null, Guests: [SomebodyElse], ReminderMinutesBeforeStart: [30],
            NotificationChannel.Push));
        await _calendarEvents.AddAsync(calendarEvent, CancellationToken.None);

        var copyId = await new DuplicateCalendarEventCommandHandler(_calendarEvents).HandleAsync(
            new DuplicateCalendarEventCommand(OwnerUserId, calendarEvent.Id, "Dentist (copy)"), CancellationToken.None);

        var copy = await _calendarEvents.GetByIdAsync(OwnerUserId, copyId!.Value, CancellationToken.None);
        Assert.Empty(copy!.Details.Guests);
        // Everything else about it is the same appointment.
        Assert.Equal("Dentist (copy)", copy.Details.Title);
        Assert.Equal("Bring the card", copy.Details.Description);
        Assert.Equal(start, copy.Details.StartUtc);
        Assert.Equal([30], copy.Details.ReminderMinutesBeforeStart);
    }

    private Task<Guid?> DuplicateNoteAsync(Guid id, string? name)
        => new DuplicateNoteCommandHandler(_notes).HandleAsync(
            new DuplicateNoteCommand(OwnerUserId, id, name), CancellationToken.None);

    private async Task<TaskList> DuplicatedTaskListAsync(Guid id, string? name)
    {
        var copyId = await new DuplicateTaskListCommandHandler(_taskLists).HandleAsync(
            new DuplicateTaskListCommand(OwnerUserId, id, name), CancellationToken.None);
        return (await _taskLists.GetByIdAsync(OwnerUserId, copyId!.Value, CancellationToken.None))!;
    }
}
