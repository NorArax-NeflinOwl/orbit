using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Inventory;
using Orbit.Core.Notes;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Transfer;
using Orbit.Core.Transfer.ExportArchive;
using Orbit.Core.Transfer.ImportArchive;
using Xunit;

namespace Orbit.Api.Tests.Transfer;

/// <summary>
/// Covers what an export promises: everything you own comes out, nothing that isn't yours does, and
/// putting the file back gives you the same things again rather than replacing what is already there.
/// </summary>
public sealed class ArchiveRoundTripTests
{
    [Fact]
    public async Task An_export_carries_every_kind_of_thing_you_own()
    {
        var context = new ArchiveTestContext();
        await context.AddNoteAsync("Shopping list", "Milk");
        await context.AddTaskListAsync("Errands", "Buy milk");
        await context.AddCalendarEventAsync("Dentist");
        await context.AddWarehouseAsync("Pantry", "Flour");

        var archive = await context.ExportAsync();

        Assert.Equal(OrbitArchive.CurrentVersion, archive.Version);
        Assert.Single(archive.Notes);
        Assert.Single(archive.TaskLists);
        Assert.Single(archive.CalendarEvents);
        Assert.Single(archive.Warehouses);
    }

    [Fact]
    public async Task Someone_elses_things_are_not_yours_to_export()
    {
        var context = new ArchiveTestContext();
        await context.AddNoteAsync("Shopping list", "Milk", ownedBySomeoneElse: true);

        var archive = await context.ExportAsync();

        // A share is access, not ownership - an export that quietly copied their note into a file would
        // be a way of taking it.
        Assert.Empty(archive.Notes);
    }

    [Fact]
    public async Task Importing_gives_you_the_things_back()
    {
        var source = new ArchiveTestContext();
        await source.AddNoteAsync("Shopping list", "Milk", "Bread");
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        var result = await destination.ImportAsync(archive);

        Assert.Equal(1, result.Notes);
        var note = Assert.Single(await destination.OwnNotesAsync());
        Assert.Equal("Shopping list", note.Title);
        Assert.Equal(["Milk", "Bread"], note.Content.Select(line => line.Text));
    }

    [Fact]
    public async Task A_task_lists_items_survive_the_round_trip()
    {
        var source = new ArchiveTestContext();
        await source.AddTaskListAsync("Errands", "Buy milk", "Buy bread");
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        var taskList = Assert.Single(await destination.OwnTaskListsAsync());
        Assert.Equal(["Buy milk", "Buy bread"], taskList.Items.Select(item => item.Description));
    }

    [Fact]
    public async Task A_link_between_two_task_lists_is_rebuilt_against_the_new_ones()
    {
        var source = new ArchiveTestContext();
        var targetId = await source.AddTaskListAsync("Groceries", "Buy milk");
        await source.AddLinkedTaskListAsync("Weekend", targetId);
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        // Ids aren't carried, so the link travels as a title and is resolved back to whichever list the
        // import just created - pointing at the old id would point at nothing.
        var imported = await destination.OwnTaskListsAsync();
        var weekend = imported.Single(taskList => taskList.Title == "Weekend");
        var groceries = imported.Single(taskList => taskList.Title == "Groceries");
        Assert.Equal(groceries.Id, Assert.Single(weekend.Items).LinkedTaskListId);
    }

    [Fact]
    public async Task A_link_to_a_list_that_did_not_come_along_is_dropped()
    {
        var source = new ArchiveTestContext();
        await source.AddLinkedTaskListAsync("Weekend", Guid.NewGuid());
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        var weekend = Assert.Single(await destination.OwnTaskListsAsync());
        Assert.Null(Assert.Single(weekend.Items).LinkedTaskListId);
    }

    [Fact]
    public async Task A_private_note_travels_sealed()
    {
        var source = new ArchiveTestContext();
        await source.AddPrivateNoteAsync();
        var archive = await source.ExportAsync();

        var archived = Assert.Single(archive.Notes);
        Assert.True(archived.IsPrivate);
        Assert.Equal(string.Empty, archived.Title);
        Assert.Equal("c2VhbGVk", archived.EncryptedContent!.Ciphertext);
    }

    [Fact]
    public async Task An_imported_private_note_is_still_private()
    {
        var source = new ArchiveTestContext();
        await source.AddPrivateNoteAsync();
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        var note = Assert.Single(await destination.OwnNotesAsync());
        Assert.True(note.IsPrivate);
        Assert.Equal("c2VhbGVk", note.EncryptedContent!.Ciphertext);
    }

    [Fact]
    public async Task A_warehouses_items_come_along_with_it()
    {
        var source = new ArchiveTestContext();
        await source.AddWarehouseAsync("Pantry", "Flour", "Sugar");
        var archive = await source.ExportAsync();

        var destination = new ArchiveTestContext();
        await destination.ImportAsync(archive);

        var warehouse = Assert.Single(await destination.OwnWarehousesAsync());
        var items = await destination.ItemsInAsync(warehouse.Id);
        Assert.Equal(["Flour", "Sugar"], items.Select(item => item.Name));
    }

    [Fact]
    public async Task Importing_adds_rather_than_replaces()
    {
        var context = new ArchiveTestContext();
        await context.AddNoteAsync("Shopping list", "Milk");
        var archive = await context.ExportAsync();

        await context.ImportAsync(archive);

        // Two copies is a mess someone can fix; an import that overwrote the wrong thing is not.
        Assert.Equal(2, (await context.OwnNotesAsync()).Count);
    }

    [Fact]
    public async Task An_archive_from_a_version_this_does_not_know_is_refused()
    {
        var context = new ArchiveTestContext();
        var archive = new OrbitArchive(Version: 99, DateTimeOffset.UtcNow, [], [], [], []);

        // Refused outright rather than read for the parts that happen to look familiar.
        await Assert.ThrowsAsync<InvalidRequestException>(() => context.ImportAsync(archive));
    }

    private sealed class ArchiveTestContext
    {
        private readonly InMemoryNoteRepository _noteRepository = new();
        private readonly InMemoryTaskRepository _taskRepository = new();
        private readonly InMemoryCalendarEventRepository _calendarEventRepository = new();
        private readonly InMemoryWarehouseRepository _warehouseRepository = new();
        private readonly InMemoryInventoryRepository _inventoryRepository = new();

        private Guid UserId { get; } = Guid.NewGuid();

        public async Task AddNoteAsync(string title, params string[] lines)
            => await AddNoteAsync(title, ownedBySomeoneElse: false, lines);

        public async Task AddNoteAsync(string title, string line, bool ownedBySomeoneElse)
            => await AddNoteAsync(title, ownedBySomeoneElse, line);

        private async Task AddNoteAsync(string title, bool ownedBySomeoneElse, params string[] lines)
        {
            var note = Note.Create(
                ownedBySomeoneElse ? Guid.NewGuid() : UserId, title, lines.Select(NoteContentLine.PlainText).ToList());
            await _noteRepository.AddAsync(note, CancellationToken.None);
        }

        public async Task AddPrivateNoteAsync()
            => await _noteRepository.AddAsync(
                Note.Create(UserId, string.Empty, [], isPrivate: true, new EncryptedPayload("c2VhbGVk", "bm9uY2U=")),
                CancellationToken.None);

        public async Task<Guid> AddTaskListAsync(string title, params string[] descriptions)
        {
            var taskList = TaskList.Create(UserId, title, descriptions.Select(Item).ToList());
            await _taskRepository.AddAsync(taskList, CancellationToken.None);
            return taskList.Id;
        }

        public async Task AddLinkedTaskListAsync(string title, Guid linkedTaskListId)
        {
            var taskList = TaskList.Create(
                UserId, title,
                [TaskItem.Create(
                    "Follows another list", null, false, linkedTaskListId, NotificationChannel.None, false,
                    NotificationChannel.None, new TimeOnly(9, 0))]);
            await _taskRepository.AddAsync(taskList, CancellationToken.None);
        }

        public async Task AddCalendarEventAsync(string title)
        {
            var details = new CalendarEventDetails(
                title, "Bring the paperwork", null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
                false, null, [], [15], NotificationChannel.None, NotificationChannel.None);
            await _calendarEventRepository.AddAsync(CalendarEvent.Create(UserId, details), CancellationToken.None);
        }

        public async Task AddWarehouseAsync(string name, params string[] itemNames)
        {
            var warehouse = Warehouse.Create(UserId, name);
            await _warehouseRepository.AddAsync(warehouse, CancellationToken.None);
            foreach (var itemName in itemNames)
            {
                await _inventoryRepository.AddAsync(
                    InventoryItem.Create(warehouse.Id, itemName, "Food", "Dry goods", 1, null, InventoryUnit.Piece, null, NotificationChannel.None),
                    CancellationToken.None);
            }
        }

        public Task<OrbitArchive> ExportAsync()
            => new ExportArchiveQueryHandler(
                    _noteRepository, _taskRepository, _calendarEventRepository, _warehouseRepository, _inventoryRepository)
                .HandleAsync(new ExportArchiveQuery(UserId), CancellationToken.None);

        public Task<ImportArchiveResult> ImportAsync(OrbitArchive archive)
            => new ImportArchiveCommandHandler(
                    _noteRepository, _taskRepository, _calendarEventRepository, _warehouseRepository, _inventoryRepository)
                .HandleAsync(new ImportArchiveCommand(UserId, archive), CancellationToken.None);

        public Task<IReadOnlyList<Note>> OwnNotesAsync() => _noteRepository.GetAllAsync(UserId, updatedSinceUtc: null, CancellationToken.None);

        public Task<IReadOnlyList<TaskList>> OwnTaskListsAsync() => _taskRepository.GetAllAsync(UserId, updatedSinceUtc: null, CancellationToken.None);

        public Task<IReadOnlyList<Warehouse>> OwnWarehousesAsync() => _warehouseRepository.GetAllAsync(UserId, updatedSinceUtc: null, CancellationToken.None);

        public Task<IReadOnlyList<InventoryItem>> ItemsInAsync(Guid warehouseId)
            => _inventoryRepository.GetAllAsync(warehouseId, CancellationToken.None);

        private static TaskItem Item(string description)
            => TaskItem.Create(
                description, null, false, null, NotificationChannel.None, false, NotificationChannel.None, new TimeOnly(9, 0));
    }
}
