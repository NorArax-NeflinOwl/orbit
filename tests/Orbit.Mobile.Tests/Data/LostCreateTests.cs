using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Tasks;
using Orbit.Core.Folders;
using Orbit.Core.Sync;
using Orbit.Mobile.Data;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Data;

/// <summary>
/// A create the outbox gave up on leaves a row with no server id. Every later edit used to queue an
/// update, which is abandoned quietly at send time on a row the server has never seen - so the thing
/// stayed on this phone for good, reading like any other. The next edit is a second try at the create
/// now, for every kind of thing, which is why each of them is here and not only notes - see LostCreates.
///
/// The give-up itself is played by emptying the queue, which is all OutboxReplay leaves behind it.
/// </summary>
public sealed class LostCreateTests
{
    private static readonly IReadOnlyList<NoteContentLineDto> SomeContent =
        [new NoteContentLineDto("Milk", false, false)];

    private static readonly IReadOnlyList<TaskItemDto> SomeItems =
    [
        new(Guid.NewGuid(), "Buy milk", null, false, null, "None", false, "None", new TimeOnly(9, 0))
    ];

    private static readonly CalendarEventDetailsDto SomeAppointment = new(
        "Dentist", null, null, null,
        DateTimeOffset.Parse("2026-09-12T09:00:00Z"), DateTimeOffset.Parse("2026-09-12T10:00:00Z"),
        false, null, [], [], ReminderNotificationChannel: "None");

    [Fact]
    public async Task Editing_a_note_whose_create_was_given_up_on_queues_the_create_again()
    {
        using var context = new StoreContext();
        var note = await context.Notes.CreateAsync("Groceries", SomeContent);
        await context.GiveUpOnEverythingQueuedAsync();

        await context.Notes.UpdateAsync(note.LocalId, new NoteContent("Groceries and bread", SomeContent, "Normal"));

        await context.AssertOnlyACreateIsQueuedAsync(SyncEntityType.Note, note.LocalId);
    }

    [Fact]
    public async Task Editing_a_task_list_whose_create_was_given_up_on_queues_the_create_again()
    {
        using var context = new StoreContext();
        var taskList = await context.TaskLists.CreateAsync("Errands", SomeItems);
        await context.GiveUpOnEverythingQueuedAsync();

        await context.TaskLists.UpdateAsync(
            taskList.LocalId, new TaskListContent("Errands today", SomeItems, IsGroup: false, "Normal"));

        await context.AssertOnlyACreateIsQueuedAsync(SyncEntityType.TaskList, taskList.LocalId);
    }

    [Fact]
    public async Task Editing_an_inventory_whose_create_was_given_up_on_queues_the_create_again()
    {
        using var context = new StoreContext();
        var inventory = await context.Inventories.CreateAsync("Pantry");
        await context.GiveUpOnEverythingQueuedAsync();

        await context.Inventories.UpdateAsync(inventory.LocalId, new InventoryContent("Larder", []));

        await context.AssertOnlyACreateIsQueuedAsync(SyncEntityType.Inventory, inventory.LocalId);
    }

    [Fact]
    public async Task Editing_an_appointment_whose_create_was_given_up_on_queues_the_create_again()
    {
        using var context = new StoreContext();
        var appointment = await context.CalendarEvents.CreateAsync(SomeAppointment);
        await context.GiveUpOnEverythingQueuedAsync();

        await context.CalendarEvents.UpdateAsync(appointment.LocalId, SomeAppointment with { Title = "Dentist, again" });

        await context.AssertOnlyACreateIsQueuedAsync(SyncEntityType.CalendarEvent, appointment.LocalId);
    }

    [Fact]
    public async Task Editing_a_place_whose_create_was_given_up_on_queues_the_create_again()
    {
        using var context = new StoreContext();
        var place = await context.Places.CreateAsync(
            new PlaceContent("Home", "", "Street 1", 52.2, 21.0, IsPrivate: false));
        await context.GiveUpOnEverythingQueuedAsync();

        await context.Places.UpdateAsync(
            place.LocalId, new PlaceContent("Home, again", "", "Street 1", 52.2, 21.0, IsPrivate: false));

        await context.AssertOnlyACreateIsQueuedAsync(SyncEntityType.Place, place.LocalId);
    }

    /// <summary>
    /// A rename used to queue nothing at all for a folder the server had never seen, trusting the create
    /// in front of it to carry the name - which is not there any more once the outbox has given up on it.
    /// </summary>
    [Fact]
    public async Task Renaming_a_folder_whose_create_was_given_up_on_queues_the_create_again()
    {
        using var context = new StoreContext();
        var folder = await context.Folders.CreateAsync("Wrok", FolderScope.Notes);
        await context.GiveUpOnEverythingQueuedAsync();

        await context.Folders.RenameAsync(folder.LocalId, "Work");

        await context.AssertOnlyACreateIsQueuedAsync(SyncEntityType.Folder, folder.LocalId);
    }

    /// <summary>
    /// Filing is an edit too, and the same trust was placed in it: a note with no server id carries its
    /// folder on its create, so filing one queued nothing.
    /// </summary>
    [Fact]
    public async Task Filing_a_note_whose_create_was_given_up_on_queues_the_create_again()
    {
        using var context = new StoreContext();
        var note = await context.Notes.CreateAsync("Groceries", SomeContent);
        var folder = await context.Folders.CreateAsync("Work", FolderScope.Notes);
        await context.GiveUpOnEverythingQueuedAsync();

        await context.Notes.FileAsync(note.LocalId, folder.LocalId);

        await context.AssertOnlyACreateIsQueuedAsync(SyncEntityType.Note, note.LocalId);
    }

    /// <summary>
    /// A create still waiting is already on its way: a second one would make two notes out of one, so
    /// the edit is queued behind it as it always was.
    /// </summary>
    [Fact]
    public async Task Editing_a_note_whose_create_is_still_queued_does_not_queue_a_second_create()
    {
        using var context = new StoreContext();
        var note = await context.Notes.CreateAsync("Groceries", SomeContent);

        await context.Notes.UpdateAsync(note.LocalId, new NoteContent("Groceries and bread", SomeContent, "Normal"));

        await using var dbContext = context.Store.CreateDbContext();
        var queued = await dbContext.Outbox.OrderBy(entry => entry.Id).ToListAsync();
        Assert.Equal([OutboxOperation.Create, OutboxOperation.Update], queued.Select(entry => entry.Operation));
    }

    /// <summary>
    /// A copy awaiting review has no server id and no create either, on purpose: the review is what
    /// sends it. Mistaking it for a lost create would push a thing nobody has decided to keep.
    /// </summary>
    [Fact]
    public async Task Filing_a_copy_awaiting_review_queues_nothing()
    {
        using var context = new StoreContext();
        var original = await context.Notes.CreateAsync("Groceries", SomeContent);
        var copy = await context.Notes.CopyForEditingAsync(original.LocalId);
        var folder = await context.Folders.CreateAsync("Work", FolderScope.Notes);
        await context.GiveUpOnEverythingQueuedAsync();

        await context.Notes.FileAsync(copy!.LocalId, folder.LocalId);

        await using var dbContext = context.Store.CreateDbContext();
        Assert.Empty(await dbContext.Outbox.ToListAsync());
    }

    private sealed class StoreContext : IDisposable
    {
        public StoreContext()
        {
            Notes = new LocalNoteRepository(Store, Clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());
            TaskLists = new LocalTaskListRepository(Store, Clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());
            Inventories = new LocalInventoryRepository(Store, Clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());
            CalendarEvents = new LocalCalendarEventRepository(Store, Clock, FixedNetworkStatus.Online);
            Places = new LocalPlaceRepository(Store, Clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());
            Folders = new LocalFolderRepository(Store, Clock);
        }

        public LocalStore Store { get; } = new();
        public FakeTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-09-11T10:00:00Z"));
        public LocalNoteRepository Notes { get; }
        public LocalTaskListRepository TaskLists { get; }
        public LocalInventoryRepository Inventories { get; }
        public LocalCalendarEventRepository CalendarEvents { get; }
        public LocalPlaceRepository Places { get; }
        public LocalFolderRepository Folders { get; }

        /// <summary>What OutboxReplay leaves after giving up: the rows, and nothing queued for them.</summary>
        public async Task GiveUpOnEverythingQueuedAsync()
        {
            await using var dbContext = Store.CreateDbContext();
            await dbContext.Outbox.ExecuteDeleteAsync();
        }

        public async Task AssertOnlyACreateIsQueuedAsync(string entityType, Guid localId)
        {
            await using var dbContext = Store.CreateDbContext();
            var queued = Assert.Single(await dbContext.Outbox.ToListAsync());
            Assert.Equal(OutboxOperation.Create, queued.Operation);
            Assert.Equal(entityType, queued.EntityType);
            Assert.Equal(localId, queued.LocalId);
        }

        public void Dispose() => Store.Dispose();
    }
}
