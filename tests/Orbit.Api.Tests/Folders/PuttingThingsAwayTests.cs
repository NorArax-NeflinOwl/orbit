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

        var archived = await new ArchiveInventoryCommandHandler(_inventories).HandleAsync(
            new ArchiveInventoryCommand(OwnerUserId, inventory.Id, IsArchived: true), CancellationToken.None);

        Assert.True(archived);
        Assert.True(inventory.IsArchived);
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
