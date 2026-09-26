using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Calendar.MoveCalendarEventToFolder;
using Orbit.Core.Folders;
using Orbit.Core.Folders.CreateFolder;
using Orbit.Core.Inventories;
using Orbit.Core.Inventories.MoveInventoryToFolder;
using Orbit.Core.Notes;
using Orbit.Core.Notes.MoveNoteToFolder;
using Orbit.Core.Notifications;
using Orbit.Core.Places;
using Orbit.Core.Places.MovePlaceToFolder;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.MoveTaskListToFolder;
using Xunit;

namespace Orbit.Api.Tests.Folders;

/// <summary>
/// Filing a note, a list, an event or an inventory. Its own command rather than a field on the update -
/// see MoveNoteToFolderCommand - and the two checks that keep a card where its owner can find it.
/// </summary>
public sealed class MoveToFolderTests
{
    private readonly InMemoryFolderRepository _folders = new();
    private readonly InMemoryNoteRepository _notes = new();
    private readonly InMemoryTaskRepository _taskLists = new();
    private readonly InMemoryCalendarEventRepository _calendarEvents = new();
    private readonly InMemoryInventoryRepository _inventories = new();
    private readonly InMemoryPlaceRepository _places = new();
    private static readonly Guid OwnerUserId = Guid.NewGuid();

    [Fact]
    public async Task A_note_is_filed_under_the_folder_it_was_sent_to()
    {
        var folder = await AFolderCalled("Work");
        var note = Note.Create(OwnerUserId, "Shopping", [NoteContentLine.PlainText("Milk")]);
        await _notes.AddAsync(note, CancellationToken.None);

        var moved = await new MoveNoteToFolderCommandHandler(_notes, _folders).HandleAsync(
            new MoveNoteToFolderCommand(OwnerUserId, note.Id, folder.Id), CancellationToken.None);

        Assert.True(moved);
        Assert.Equal(folder.Id, note.FolderId);
    }

    /// <summary>
    /// Null is how something is taken out of a folder, and it lands back in the built-in one its own
    /// privacy decides - see BuiltInFolder. It is not "leave it alone", which is why filing travels on
    /// its own request instead of on the update.
    /// </summary>
    [Fact]
    public async Task Filing_a_note_under_nothing_takes_it_out_of_the_folder()
    {
        var folder = await AFolderCalled("Work");
        var note = Note.Create(OwnerUserId, "Shopping", [NoteContentLine.PlainText("Milk")], folderId: folder.Id);
        await _notes.AddAsync(note, CancellationToken.None);

        await new MoveNoteToFolderCommandHandler(_notes, _folders).HandleAsync(
            new MoveNoteToFolderCommand(OwnerUserId, note.Id, FolderId: null), CancellationToken.None);

        Assert.Null(note.FolderId);
    }

    /// <summary>
    /// A folder id arrives from a client, so one belonging to somebody else has to be refused: filing a
    /// note under a tab its owner cannot see is the same thing as losing it.
    /// </summary>
    [Fact]
    public async Task A_note_is_not_filed_under_somebody_elses_folder()
    {
        var theirFolder = await new CreateFolderCommandHandler(_folders).HandleAsync(
            new CreateFolderCommand(Guid.NewGuid(), "Theirs", FolderScope.Notes), CancellationToken.None);
        var note = Note.Create(OwnerUserId, "Shopping", [NoteContentLine.PlainText("Milk")]);
        await _notes.AddAsync(note, CancellationToken.None);

        var moved = await new MoveNoteToFolderCommandHandler(_notes, _folders).HandleAsync(
            new MoveNoteToFolderCommand(OwnerUserId, note.Id, theirFolder.Id), CancellationToken.None);

        Assert.False(moved);
        Assert.Null(note.FolderId);
    }

    [Fact]
    public async Task A_task_list_is_filed_the_same_way()
    {
        var folder = await AFolderCalled("Work");
        var taskList = TaskList.Create(OwnerUserId, "Moving", []);
        await _taskLists.AddAsync(taskList, CancellationToken.None);

        var moved = await new MoveTaskListToFolderCommandHandler(_taskLists, _folders).HandleAsync(
            new MoveTaskListToFolderCommand(OwnerUserId, taskList.Id, folder.Id), CancellationToken.None);

        Assert.True(moved);
        Assert.Equal(folder.Id, taskList.FolderId);
    }

    [Fact]
    public async Task A_list_that_is_not_this_readers_is_not_theirs_to_file()
    {
        var folder = await AFolderCalled("Work");
        var taskList = TaskList.Create(Guid.NewGuid(), "Theirs", []);
        await _taskLists.AddAsync(taskList, CancellationToken.None);

        var moved = await new MoveTaskListToFolderCommandHandler(_taskLists, _folders).HandleAsync(
            new MoveTaskListToFolderCommand(OwnerUserId, taskList.Id, folder.Id), CancellationToken.None);

        Assert.False(moved);
        Assert.Null(taskList.FolderId);
    }

    [Fact]
    public async Task An_event_is_filed_the_same_way()
    {
        var folder = await AFolderCalled("Work");
        var calendarEvent = CalendarEvent.Create(OwnerUserId, Appointment());
        await _calendarEvents.AddAsync(calendarEvent, CancellationToken.None);

        var moved = await new MoveCalendarEventToFolderCommandHandler(_calendarEvents, _folders).HandleAsync(
            new MoveCalendarEventToFolderCommand(OwnerUserId, calendarEvent.Id, folder.Id), CancellationToken.None);

        Assert.True(moved);
        Assert.Equal(folder.Id, calendarEvent.FolderId);
    }

    [Fact]
    public async Task An_event_is_not_filed_under_somebody_elses_folder()
    {
        var theirFolder = await new CreateFolderCommandHandler(_folders).HandleAsync(
            new CreateFolderCommand(Guid.NewGuid(), "Theirs", FolderScope.Calendar), CancellationToken.None);
        var calendarEvent = CalendarEvent.Create(OwnerUserId, Appointment());
        await _calendarEvents.AddAsync(calendarEvent, CancellationToken.None);

        var moved = await new MoveCalendarEventToFolderCommandHandler(_calendarEvents, _folders).HandleAsync(
            new MoveCalendarEventToFolderCommand(OwnerUserId, calendarEvent.Id, theirFolder.Id), CancellationToken.None);

        Assert.False(moved);
        Assert.Null(calendarEvent.FolderId);
    }

    [Fact]
    public async Task An_inventory_is_filed_the_same_way()
    {
        var folder = await AFolderCalled("Kitchen");
        var inventory = Inventory.Create(OwnerUserId, "Pantry");
        await _inventories.AddAsync(inventory, CancellationToken.None);

        var moved = await new MoveInventoryToFolderCommandHandler(_inventories, _folders).HandleAsync(
            new MoveInventoryToFolderCommand(OwnerUserId, inventory.Id, folder.Id), CancellationToken.None);

        Assert.True(moved);
        Assert.Equal(folder.Id, inventory.FolderId);
    }

    /// <summary>
    /// A private inventory is filed like any other: the folder sits outside the sealed half, so the
    /// server can move it without a key - see Inventory.FolderId.
    /// </summary>
    [Fact]
    public async Task A_private_inventory_is_filed_without_opening_it()
    {
        var folder = await AFolderCalled("Kitchen");
        var inventory = Inventory.Create(
            OwnerUserId, string.Empty, isPrivate: true, encryptedContent: new EncryptedPayload("c2VhbGVk", "bm9uY2U="));
        await _inventories.AddAsync(inventory, CancellationToken.None);

        var moved = await new MoveInventoryToFolderCommandHandler(_inventories, _folders).HandleAsync(
            new MoveInventoryToFolderCommand(OwnerUserId, inventory.Id, folder.Id), CancellationToken.None);

        Assert.True(moved);
        Assert.Equal(folder.Id, inventory.FolderId);
        Assert.True(inventory.IsPrivate);
        Assert.Equal(string.Empty, inventory.Name);
    }

    [Fact]
    public async Task An_inventory_that_is_not_this_readers_is_not_theirs_to_file()
    {
        var folder = await AFolderCalled("Kitchen");
        var inventory = Inventory.Create(Guid.NewGuid(), "Theirs");
        await _inventories.AddAsync(inventory, CancellationToken.None);

        var moved = await new MoveInventoryToFolderCommandHandler(_inventories, _folders).HandleAsync(
            new MoveInventoryToFolderCommand(OwnerUserId, inventory.Id, folder.Id), CancellationToken.None);

        Assert.False(moved);
        Assert.Null(inventory.FolderId);
    }

    /// <summary>
    /// The fifth kind, since 2026-09-26 - see Place.FolderId. A place is sealed unless its owner says
    /// otherwise, and this is the ordinary one for completeness; the sealed case is below, and it is the
    /// one that matters here.
    /// </summary>
    [Fact]
    public async Task A_place_is_filed_the_same_way()
    {
        var folder = await AFolderCalled("Holiday");
        var place = Place.Create(OwnerUserId, "Bakery", string.Empty, Somewhere(), isPrivate: false);
        await _places.AddAsync(place, CancellationToken.None);

        var moved = await new MovePlaceToFolderCommandHandler(_places, _folders).HandleAsync(
            new MovePlaceToFolderCommand(OwnerUserId, place.Id, folder.Id), CancellationToken.None);

        Assert.True(moved);
        Assert.Equal(folder.Id, place.FolderId);
    }

    /// <summary>
    /// And a sealed place is filed without being opened, which is what makes folders worth having on the
    /// map at all: most places are sealed, and the server holds no key. The folder sits outside the
    /// sealed half, exactly as a private inventory's does.
    /// </summary>
    [Fact]
    public async Task A_sealed_place_is_filed_without_opening_it()
    {
        var folder = await AFolderCalled("Holiday");
        var place = Place.Create(
            OwnerUserId, string.Empty, string.Empty, new EventLocation(null, 0, 0), isPrivate: true,
            encryptedContent: new EncryptedPayload("c2VhbGVk", "bm9uY2U="));
        await _places.AddAsync(place, CancellationToken.None);

        var moved = await new MovePlaceToFolderCommandHandler(_places, _folders).HandleAsync(
            new MovePlaceToFolderCommand(OwnerUserId, place.Id, folder.Id), CancellationToken.None);

        Assert.True(moved);
        Assert.Equal(folder.Id, place.FolderId);
        Assert.True(place.IsPrivate);
        Assert.Equal(string.Empty, place.Name);
    }

    [Fact]
    public async Task A_place_that_is_not_this_readers_is_not_theirs_to_file()
    {
        var folder = await AFolderCalled("Holiday");
        var place = Place.Create(Guid.NewGuid(), "Theirs", string.Empty, Somewhere(), isPrivate: false);
        await _places.AddAsync(place, CancellationToken.None);

        var moved = await new MovePlaceToFolderCommandHandler(_places, _folders).HandleAsync(
            new MovePlaceToFolderCommand(OwnerUserId, place.Id, folder.Id), CancellationToken.None);

        Assert.False(moved);
        Assert.Null(place.FolderId);
    }

    private static EventLocation Somewhere() => new("A street", 52.23, 21.01);

    private static CalendarEventDetails Appointment()
        => new(
            "Dentist", "Bring the paperwork", null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
            false, null, [], [15], NotificationChannel.None);

    private Task<Folder> AFolderCalled(string name)
        => new CreateFolderCommandHandler(_folders).HandleAsync(
            new CreateFolderCommand(OwnerUserId, name, FolderScope.Notes), CancellationToken.None);
}
