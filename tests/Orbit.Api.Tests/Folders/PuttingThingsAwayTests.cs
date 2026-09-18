using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.ArchiveCalendarEvent;
using Orbit.Core.Folders;
using Orbit.Core.Folders.CreateFolder;
using Orbit.Core.Inventories;
using Orbit.Core.Inventories.ArchiveInventory;
using Orbit.Core.Notes;
using Orbit.Core.Notes.ArchiveNote;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.ArchiveTaskList;
using Xunit;

namespace Orbit.Api.Tests.Folders;

/// <summary>
/// Putting something away rather than deleting it - the fourth built-in folder, and the only one with a
/// column behind it (see BuiltInFolder.Archived). Its own command on each of the four kinds, for the
/// reason filing has one: an update carries the whole thing.
/// </summary>
public sealed class PuttingThingsAwayTests
{
    private readonly InMemoryFolderRepository _folders = new();
    private readonly InMemoryNoteRepository _notes = new();
    private readonly InMemoryTaskRepository _taskLists = new();
    private readonly InMemoryCalendarEventRepository _calendarEvents = new();
    private readonly InMemoryInventoryRepository _inventories = new();
    private static readonly Guid OwnerUserId = Guid.NewGuid();

    [Fact]
    public async Task A_note_is_put_away_and_brought_back_by_the_same_command()
    {
        var note = Note.Create(OwnerUserId, "Shopping", [NoteContentLine.PlainText("Milk")]);
        await _notes.AddAsync(note, CancellationToken.None);

        Assert.True(await ArchiveTheNoteAsync(note.Id, isArchived: true));
        Assert.True(note.IsArchived);

        Assert.True(await ArchiveTheNoteAsync(note.Id, isArchived: false));
        Assert.False(note.IsArchived);
    }

    /// <summary>
    /// The folder it was filed under is left alone, which is what makes bringing it back put it where it
    /// was rather than somewhere a rule had to choose - see Note.Archive.
    /// </summary>
    [Fact]
    public async Task Putting_a_note_away_keeps_the_folder_it_was_filed_under()
    {
        var folder = await AFolderCalled("Work");
        var note = Note.Create(OwnerUserId, "Shopping", [NoteContentLine.PlainText("Milk")]);
        note.MoveToFolder(folder.Id);
        await _notes.AddAsync(note, CancellationToken.None);

        await ArchiveTheNoteAsync(note.Id, isArchived: true);

        Assert.Equal(folder.Id, note.FolderId);
        // And it is under Archived while it is away, although it is still filed under Work.
        Assert.Equal(
            FolderKey.Of(BuiltInFolder.Archived),
            FolderPlacement.Of(note.FolderId, note.IsPrivate, isFinished: false, [folder.Id], note.IsArchived));

        await ArchiveTheNoteAsync(note.Id, isArchived: false);

        Assert.Equal(
            FolderKey.Of(folder.Id),
            FolderPlacement.Of(note.FolderId, note.IsPrivate, isFinished: false, [folder.Id], note.IsArchived));
    }

    /// <summary>Somebody else's is not theirs to put away - one row is one note, so it would vanish from its owner's pages.</summary>
    [Fact]
    public async Task Somebody_elses_note_is_not_put_away()
    {
        var note = Note.Create(Guid.NewGuid(), "Theirs", [NoteContentLine.PlainText("Milk")]);
        await _notes.AddAsync(note, CancellationToken.None);

        Assert.False(await ArchiveTheNoteAsync(note.Id, isArchived: true));
        Assert.False(note.IsArchived);
    }

    [Fact]
    public async Task A_note_that_is_not_there_is_not_put_away()
        => Assert.False(await ArchiveTheNoteAsync(Guid.NewGuid(), isArchived: true));

    [Fact]
    public async Task A_task_list_is_put_away()
    {
        var taskList = TaskList.Create(OwnerUserId, "Errands", [TaskItem.Create("Milk", null, false)]);
        await _taskLists.AddAsync(taskList, CancellationToken.None);

        var archived = await new ArchiveTaskListCommandHandler(_taskLists).HandleAsync(
            new ArchiveTaskListCommand(OwnerUserId, taskList.Id, IsArchived: true), CancellationToken.None);

        Assert.True(archived);
        Assert.True(taskList.IsArchived);
    }

    /// <summary>
    /// And beats Finished, which it would otherwise have gathered under: a list that is both done and put
    /// away is put away, because that is the later decision and the one about whether it is in front of
    /// the reader at all.
    /// </summary>
    [Fact]
    public async Task A_finished_list_that_is_put_away_is_under_Archived()
    {
        var taskList = TaskList.Create(OwnerUserId, "Errands", [TaskItem.Create("Milk", null, true)]);
        await _taskLists.AddAsync(taskList, CancellationToken.None);

        await new ArchiveTaskListCommandHandler(_taskLists).HandleAsync(
            new ArchiveTaskListCommand(OwnerUserId, taskList.Id, IsArchived: true), CancellationToken.None);

        Assert.True(taskList.IsCompleted);
        Assert.Equal(
            FolderKey.Of(BuiltInFolder.Archived),
            FolderPlacement.Of(taskList.FolderId, taskList.IsPrivate, taskList.IsCompleted, [], taskList.IsArchived));
    }

    [Fact]
    public async Task An_appointment_is_put_away()
    {
        var calendarEvent = CalendarEvent.Create(OwnerUserId, Appointment());
        await _calendarEvents.AddAsync(calendarEvent, CancellationToken.None);

        var archived = await new ArchiveCalendarEventCommandHandler(_calendarEvents).HandleAsync(
            new ArchiveCalendarEventCommand(OwnerUserId, calendarEvent.Id, IsArchived: true), CancellationToken.None);

        Assert.True(archived);
        Assert.True(calendarEvent.IsArchived);
    }

    [Fact]
    public async Task A_shelf_is_put_away()
    {
        var inventory = Inventory.Create(OwnerUserId, "Pantry");
        await _inventories.AddAsync(inventory, CancellationToken.None);

        var archived = await new ArchiveInventoryCommandHandler(_inventories, _taskLists).HandleAsync(
            new ArchiveInventoryCommand(OwnerUserId, inventory.Id, IsArchived: true), CancellationToken.None);

        Assert.True(archived);
        Assert.True(inventory.IsArchived);
    }

    /// <summary>
    /// The same rule for a list: putting one away takes it out of every group gathering it. Confirmed by
    /// the user on 2026-09-18 - a group went on standing for a list its owner had filed out of sight,
    /// counting its work into its own progress with nothing on screen saying why.
    /// </summary>
    [Fact]
    public async Task A_list_put_away_comes_out_of_the_groups_gathering_it()
    {
        var shopping = TaskList.Create(OwnerUserId, "Shopping", []);
        await _taskLists.AddAsync(shopping, CancellationToken.None);
        var week = TaskList.Create(
            OwnerUserId, "This week",
            [TaskItem.Create("Shopping", null, false, linkedTaskListIds: [shopping.Id])]);
        await _taskLists.AddAsync(week, CancellationToken.None);

        await new ArchiveTaskListCommandHandler(_taskLists).HandleAsync(
            new ArchiveTaskListCommand(OwnerUserId, shopping.Id, IsArchived: true), CancellationToken.None);

        // The row existed to point at it, so it goes with the pointing - see TaskList.StopGathering.
        Assert.Empty(week.Items);
    }

    /// <summary>
    /// A row standing for two lists keeps the other one. Only what it stood for is taken away, not the
    /// row: it is still a pointer, and still says something.
    /// </summary>
    [Fact]
    public async Task A_row_standing_for_two_lists_keeps_the_one_that_stayed()
    {
        var shopping = TaskList.Create(OwnerUserId, "Shopping", []);
        var chores = TaskList.Create(OwnerUserId, "Chores", []);
        await _taskLists.AddAsync(shopping, CancellationToken.None);
        await _taskLists.AddAsync(chores, CancellationToken.None);
        var week = TaskList.Create(
            OwnerUserId, "This week",
            [TaskItem.Create("Errands", null, false, linkedTaskListIds: [shopping.Id, chores.Id])]);
        await _taskLists.AddAsync(week, CancellationToken.None);

        await new ArchiveTaskListCommandHandler(_taskLists).HandleAsync(
            new ArchiveTaskListCommand(OwnerUserId, shopping.Id, IsArchived: true), CancellationToken.None);

        Assert.Equal([chores.Id], Assert.Single(week.Items).LinkedTaskListIds);
    }

    /// <summary>
    /// Bringing it back does not put it back into the groups: nothing records which they were, and
    /// guessing would be writing a row nobody wrote.
    /// </summary>
    [Fact]
    public async Task And_bringing_a_list_back_does_not_put_it_back_in_them()
    {
        var shopping = TaskList.Create(OwnerUserId, "Shopping", []);
        await _taskLists.AddAsync(shopping, CancellationToken.None);
        var week = TaskList.Create(
            OwnerUserId, "This week",
            [TaskItem.Create("Shopping", null, false, linkedTaskListIds: [shopping.Id])]);
        await _taskLists.AddAsync(week, CancellationToken.None);
        var handler = new ArchiveTaskListCommandHandler(_taskLists);

        await handler.HandleAsync(
            new ArchiveTaskListCommand(OwnerUserId, shopping.Id, IsArchived: true), CancellationToken.None);
        await handler.HandleAsync(
            new ArchiveTaskListCommand(OwnerUserId, shopping.Id, IsArchived: false), CancellationToken.None);

        Assert.Empty(week.Items);
        Assert.False(shopping.IsArchived);
    }

    /// <summary>A list nothing gathers is put away and nothing else is touched.</summary>
    [Fact]
    public async Task A_list_nothing_gathers_leaves_the_others_alone()
    {
        var shopping = TaskList.Create(OwnerUserId, "Shopping", []);
        await _taskLists.AddAsync(shopping, CancellationToken.None);
        var week = TaskList.Create(OwnerUserId, "This week", [TaskItem.Create("Hoover", null, false)]);
        await _taskLists.AddAsync(week, CancellationToken.None);

        await new ArchiveTaskListCommandHandler(_taskLists).HandleAsync(
            new ArchiveTaskListCommand(OwnerUserId, shopping.Id, IsArchived: true), CancellationToken.None);

        Assert.Single(week.Items);
    }

    /// <summary>
    /// And goes off the lists it was measured against. Asked for on 2026-09-18: putting a shelf away
    /// says it is done with, and a list still measured against it went on showing a stock check against
    /// a shelf its owner had filed out of sight - and went on raising restock errands from it.
    /// </summary>
    [Fact]
    public async Task A_shelf_put_away_comes_off_the_lists_measured_against_it()
    {
        var inventory = Inventory.Create(OwnerUserId, "Pantry");
        await _inventories.AddAsync(inventory, CancellationToken.None);
        var shopping = TaskList.Create(OwnerUserId, "Shopping", []);
        shopping.LinkToInventory(inventory.Id);
        await _taskLists.AddAsync(shopping, CancellationToken.None);

        await new ArchiveInventoryCommandHandler(_inventories, _taskLists).HandleAsync(
            new ArchiveInventoryCommand(OwnerUserId, inventory.Id, IsArchived: true), CancellationToken.None);

        Assert.Null(shopping.LinkedInventoryId);
    }

    /// <summary>
    /// Bringing it back does not put the links back: nothing records which lists they were, and guessing
    /// would be inventing a choice nobody made. Choosing it again is one press.
    /// </summary>
    [Fact]
    public async Task And_bringing_it_back_does_not_put_them_back()
    {
        var inventory = Inventory.Create(OwnerUserId, "Pantry");
        await _inventories.AddAsync(inventory, CancellationToken.None);
        var shopping = TaskList.Create(OwnerUserId, "Shopping", []);
        shopping.LinkToInventory(inventory.Id);
        await _taskLists.AddAsync(shopping, CancellationToken.None);
        var handler = new ArchiveInventoryCommandHandler(_inventories, _taskLists);

        await handler.HandleAsync(
            new ArchiveInventoryCommand(OwnerUserId, inventory.Id, IsArchived: true), CancellationToken.None);
        await handler.HandleAsync(
            new ArchiveInventoryCommand(OwnerUserId, inventory.Id, IsArchived: false), CancellationToken.None);

        Assert.Null(shopping.LinkedInventoryId);
        Assert.False(inventory.IsArchived);
    }

    /// <summary>Somebody else's list is not this owner's to unpick - the lists read are their own.</summary>
    [Fact]
    public async Task Another_readers_list_is_left_alone()
    {
        var inventory = Inventory.Create(OwnerUserId, "Pantry");
        await _inventories.AddAsync(inventory, CancellationToken.None);
        var theirs = TaskList.Create(Guid.NewGuid(), "Shopping", []);
        theirs.LinkToInventory(inventory.Id);
        await _taskLists.AddAsync(theirs, CancellationToken.None);

        await new ArchiveInventoryCommandHandler(_inventories, _taskLists).HandleAsync(
            new ArchiveInventoryCommand(OwnerUserId, inventory.Id, IsArchived: true), CancellationToken.None);

        Assert.Equal(inventory.Id, theirs.LinkedInventoryId);
    }

    /// <summary>Saying again what is already so changes nothing, and does not restamp when it was changed.</summary>
    [Fact]
    public async Task Putting_away_what_is_already_away_does_not_touch_it()
    {
        var note = Note.Create(OwnerUserId, "Shopping", [NoteContentLine.PlainText("Milk")]);
        await _notes.AddAsync(note, CancellationToken.None);
        await ArchiveTheNoteAsync(note.Id, isArchived: true);
        var changedAt = note.UpdatedAtUtc;

        await ArchiveTheNoteAsync(note.Id, isArchived: true);

        Assert.Equal(changedAt, note.UpdatedAtUtc);
    }

    private Task<bool> ArchiveTheNoteAsync(Guid noteId, bool isArchived)
        => new ArchiveNoteCommandHandler(_notes).HandleAsync(
            new ArchiveNoteCommand(OwnerUserId, noteId, isArchived), CancellationToken.None);

    private static CalendarEventDetails Appointment()
        => new(
            "Dentist", "Bring the paperwork", null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
            false, null, [], [15], NotificationChannel.None);

    private Task<Folder> AFolderCalled(string name)
        => new CreateFolderCommandHandler(_folders).HandleAsync(
            new CreateFolderCommand(OwnerUserId, name, FolderScope.Notes), CancellationToken.None);
}
