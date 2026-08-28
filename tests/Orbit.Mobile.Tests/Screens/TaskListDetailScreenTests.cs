using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Tasks;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Screens.Tasks;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Chat;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The task-list screen, driven the way a reader drives it: type, add, tick.
///
/// These exist because a screen holds state of its own - what it last read - and that is where a bug got
/// through. Every other test here works one layer down, on stores and synchronisers, which were right;
/// what was wrong was the screen keeping a copy that the sync had already made stale.
/// </summary>
public sealed class TaskListDetailScreenTests
{
    [Fact]
    public async Task Ticking_an_entry_just_added_keeps_the_id_the_server_gave_it()
    {
        // The bug this stands for: an entry added here has no server id until the push comes back with
        // one. The screen read the list before syncing and never again, so the tick was built on the
        // copy that still had none - and the server made a second entry and dropped the first, cutting
        // loose an inventory item's restock task, a reminder's "already sent today" record, an overdue
        // notice. Everything below the screen was correct; only the screen was stale.
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Groceries");
        screen.NewItemDescription = "Buy milk";
        await screen.AddItemCommand.ExecuteAsync(null);

        var entryId = Assert.Single(context.Server.TaskLists.Single().Items).Id;
        await screen.ToggleItemCommand.ExecuteAsync(Assert.Single(screen.Items));

        var entry = Assert.Single(context.Server.TaskLists.Single().Items);
        Assert.Equal(entryId, entry.Id);
        Assert.True(entry.IsCompleted);
    }

    [Fact]
    public async Task An_entry_added_offline_reaches_the_server_with_one_id_once_the_connection_returns()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Groceries");
        await context.SynchroniseAsync();

        context.Server.IsUnreachable = true;
        screen.NewItemDescription = "Buy milk";
        await screen.AddItemCommand.ExecuteAsync(null);
        Assert.Equal("Saved on this phone - it will sync later", screen.Status);

        context.Server.IsUnreachable = false;
        await screen.ToggleItemCommand.ExecuteAsync(Assert.Single(screen.Items));
        await context.SynchroniseAsync();

        var entry = Assert.Single(context.Server.TaskLists.Single().Items);
        Assert.Equal("Buy milk", entry.Description);
        Assert.True(entry.IsCompleted);
    }

    [Fact]
    public async Task Removing_an_entry_leaves_the_others_with_the_ids_they_had()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Groceries");
        foreach (var description in new[] { "Buy milk", "Buy bread" })
        {
            screen.NewItemDescription = description;
            await screen.AddItemCommand.ExecuteAsync(null);
        }

        var breadId = context.Server.TaskLists.Single().Items.Single(item => item.Description == "Buy bread").Id;
        await screen.RemoveItemCommand.ExecuteAsync(screen.Items.Single(row => row.Description == "Buy milk"));

        var remaining = Assert.Single(context.Server.TaskLists.Single().Items);
        Assert.Equal(breadId, remaining.Id);
    }

    /// <summary>
    /// The rest of an entry, which the phone could neither see nor set: a due date, what happens when
    /// it passes, and whether it says something every day until the entry is done.
    /// </summary>
    [Fact]
    public async Task An_entrys_due_date_and_reminders_can_be_set()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Trip");
        screen.NewItemDescription = "pack";
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.HasDueDate = true;
        screen.BeingEdited.DueDate = new DateTime(2027, 3, 1);
        screen.BeingEdited.OverdueNotificationChannel = "Push";
        screen.BeingEdited.RemindDaily = true;
        await screen.SaveItemCommand.ExecuteAsync(null);

        var item = Assert.Single(screen.Items).Item;
        Assert.Equal(new DateTime(2027, 3, 1), item.DueDateUtc!.Value.LocalDateTime.Date);
        Assert.Equal("Push", item.OverdueNotificationChannel);
        Assert.True(item.RemindDaily);
    }

    /// <summary>
    /// Everything the editor does not show travels through untouched. An entry linked to an inventory
    /// item's restock task must come back linked, or the shelf loses its reminder.
    /// </summary>
    [Fact]
    public async Task Editing_an_entry_keeps_what_the_editor_does_not_show()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Trip");
        screen.NewItemDescription = "pack";
        await screen.AddItemCommand.ExecuteAsync(null);
        await screen.ToggleItemCommand.ExecuteAsync(screen.Items[0]);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Description = "pack properly";
        await screen.SaveItemCommand.ExecuteAsync(null);

        var item = Assert.Single(screen.Items).Item;
        Assert.Equal("pack properly", item.Description);
        Assert.True(item.IsCompleted);
    }

    /// <summary>A finished entry cannot be late any more, whatever its date says.</summary>
    [Fact]
    public async Task A_completed_entry_is_never_overdue()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Trip");
        screen.NewItemDescription = "pack";
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.HasDueDate = true;
        screen.BeingEdited.DueDate = new DateTime(2020, 1, 1);
        await screen.SaveItemCommand.ExecuteAsync(null);
        Assert.True(screen.Items[0].IsOverdue);

        await screen.ToggleItemCommand.ExecuteAsync(screen.Items[0]);

        Assert.False(screen.Items[0].IsOverdue);
    }

    /// <summary>
    /// Orbit.Web has a "Group list" checkbox; the phone had no way to set it, so a list made here
    /// could never be one - and the stock check, which only a group list is asked, was unreachable.
    /// </summary>
    [Fact]
    public async Task A_list_can_be_made_a_group_list()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Trip");

        Assert.False(screen.IsGroup);
        screen.IsGroup = true;
        // The switch starts the save rather than awaiting it, so the test waits on what it started -
        // asserting straight afterwards would be racing the write it is meant to be checking.
        await screen.SaveListCommand.ExecutionTask!;
        await context.SynchroniseAsync();

        Assert.Contains(context.Server.TaskLists, list => list.Title == "Trip" && list.IsGroup);
    }

    /// <summary>
    /// Orbit.Web's task editor has a Title field. This screen showed the title and would not let
    /// anybody change it - so a list named wrongly stayed named wrongly.
    /// </summary>
    [Fact]
    public async Task A_list_can_be_renamed()
    {
        using var context = new ScreenContext();
        var screen = context.OpenTaskList("Toady");

        screen.Title = "Today";
        await screen.SaveListCommand.ExecuteAsync(null);
        await context.SynchroniseAsync();

        Assert.Contains("Today", context.Server.TaskLists.Select(list => list.Title));
    }

    /// <summary>
    /// Moving an entry to another list, which the phone could not do at all. It is a change to two
    /// lists rather than to the entry, so it happens on choosing rather than on the form's Save - the
    /// same as Orbit.Web's editor.
    /// </summary>
    [Fact]
    public async Task An_entry_can_be_moved_to_another_list()
    {
        using var context = new ScreenContext();
        context.OpenTaskList("Later");
        var screen = context.OpenTaskList("Today");
        screen.NewItemDescription = "Call the plumber";
        await screen.AddItemCommand.ExecuteAsync(null);
        await context.SynchroniseAsync();
        screen.LoadCommand.Execute(null);

        screen.EditItemCommand.Execute(screen.Items.Single());
        var later = screen.MoveTargets.Single(target => target.Name == "Later");
        await screen.MoveItemCommand.ExecuteAsync(later);

        Assert.Empty(screen.Items);
        Assert.Contains("Later", screen.Status);
        Assert.Contains(
            "Call the plumber",
            context.Server.ItemsIn(later.ServerId).Select(item => item.Description));
    }

    /// <summary>The list being looked at is not somewhere its own entries can go.</summary>
    [Fact]
    public async Task The_list_being_looked_at_is_not_one_of_the_places_to_move_to()
    {
        using var context = new ScreenContext();
        context.OpenTaskList("Later");
        var screen = context.OpenTaskList("Today");
        await context.SynchroniseAsync();
        screen.LoadCommand.Execute(null);

        Assert.DoesNotContain(screen.MoveTargets, target => target.Name == "Today");
        Assert.Contains(screen.MoveTargets, target => target.Name == "Later");
    }

    /// <summary>
    /// An entry added on this phone has no id the server would recognise until it syncs, and offline
    /// there is nobody to do the moving. Neither is an error worth showing - the choice just isn't there.
    /// </summary>
    [Fact]
    public async Task An_entry_the_server_has_never_seen_cannot_be_moved()
    {
        using var context = new ScreenContext();
        context.OpenTaskList("Later");
        var screen = context.OpenTaskList("Today");
        await context.SynchroniseAsync();
        screen.LoadCommand.Execute(null);

        screen.NewItemDescription = "Call the plumber";
        context.Server.IsUnreachable = true;
        await screen.AddItemCommand.ExecuteAsync(null);

        screen.EditItemCommand.Execute(screen.Items.Single());

        Assert.False(screen.CanMoveItem);
    }

    /// <summary>A phone with a local store and a server it can sometimes reach, and no MAUI in sight.</summary>
    private sealed class ScreenContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
        private readonly LocalTaskListRepository _taskLists;

        public ScreenContext()
        {
            Server = new FakeTasksServer(_clock);
            _taskLists = new LocalTaskListRepository(_localStore, _clock, FixedNetworkStatus.Online);
            StockCheck = new StockCheckPanel(
                new TasksClient(Server.ToHttpClient()),
                new LocalWarehouseRepository(_localStore, _clock, FixedNetworkStatus.Online),
                new Translations(new InMemoryLanguageStore()));
            Synchronizer = new TaskListSynchronizer(
                _localStore, new TasksClient(Server.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<TaskListSynchronizer>.Instance);
        }

        public FakeTasksServer Server { get; }

        /// <summary>"Can this be done?" - see StockCheckPanel.</summary>
        public StockCheckPanel StockCheck { get; private set; } = null!;

        public TaskListSynchronizer Synchronizer { get; }

        public RecordingScreenNavigator Navigator { get; } = new();

        public TaskListDetailViewModel OpenTaskList(string title)
        {
            var created = _taskLists.CreateAsync(title, []).GetAwaiter().GetResult();
            var screen = new TaskListDetailViewModel(
                _taskLists, Synchronizer, new Translations(new InMemoryLanguageStore()), _clock,
                ShareTestPanel.For(_localStore, new ChatRepository(_localStore, _clock)), Navigator,
                new TasksClient(Server.ToHttpClient()), NothingIsBeingEdited(_clock), FixedNetworkStatus.Online,
                StockCheck);
            screen.Open(created.LocalId);
            screen.LoadCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            return screen;
        }

        public Task<SyncResult> SynchroniseAsync() => Synchronizer.SynchroniseAsync(CancellationToken.None);

        /// <summary>
        /// A lock over a fake server that answers every claim with "yours" - these tests are about the
        /// editor, and EditLockTests covers what happens when somebody else is in it.
        /// </summary>
        private static EditLock NothingIsBeingEdited(TimeProvider clock)
            => new(FixedNetworkStatus.Online, clock, new Translations(new InMemoryLanguageStore()));

        public void Dispose()
        {
            Server.Dispose();
            _localStore.Dispose();
        }
    }
}
