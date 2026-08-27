using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Tasks;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Screens.Tasks;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;
using Orbit.Mobile.Localization;

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
            Synchronizer = new TaskListSynchronizer(
                _localStore, new TasksClient(Server.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<TaskListSynchronizer>.Instance);
        }

        public FakeTasksServer Server { get; }

        public TaskListSynchronizer Synchronizer { get; }

        public RecordingScreenNavigator Navigator { get; } = new();

        public TaskListDetailViewModel OpenTaskList(string title)
        {
            var created = _taskLists.CreateAsync(title, []).GetAwaiter().GetResult();
            var screen = new TaskListDetailViewModel(
                _taskLists, Synchronizer, new Translations(new InMemoryLanguageStore()), Navigator);
            screen.Open(created.LocalId);
            screen.LoadCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            return screen;
        }

        public Task<SyncResult> SynchroniseAsync() => Synchronizer.SynchroniseAsync(CancellationToken.None);

        public void Dispose()
        {
            Server.Dispose();
            _localStore.Dispose();
        }
    }
}
