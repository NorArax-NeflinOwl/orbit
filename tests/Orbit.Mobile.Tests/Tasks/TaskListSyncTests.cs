using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Tasks;
using Orbit.Core.Sync;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Tasks;

/// <summary>
/// Task lists on the sync spine - the second entity type, and the one that decides whether the spine
/// generalised or merely worked once. The interesting new risk is the shared outbox: notes and task
/// lists queue into one table, in one order, so a change to one must never be sent as, or block, the
/// other.
/// </summary>
public sealed class TaskListSyncTests
{
    private static readonly IReadOnlyList<TaskItemDto> SomeItems =
    [
        new(Guid.NewGuid(), "Buy milk", null, false, null, "None", false, "None", new TimeOnly(9, 0))
    ];

    [Fact]
    public async Task A_list_written_offline_reaches_the_server_when_the_connection_returns()
    {
        using var context = new TaskContext();
        context.GoOffline();
        await context.TaskLists.CreateAsync("Groceries", SomeItems);

        context.ComeBackOnline();
        var result = await context.SynchroniseAsync();

        Assert.Equal(1, result.Sent);
        Assert.Contains(context.Server.TaskLists, list => list.Title == "Groceries");
    }

    [Fact]
    public async Task Items_survive_the_round_trip_through_the_local_database()
    {
        using var context = new TaskContext();

        var created = await context.TaskLists.CreateAsync("Groceries", SomeItems);

        // Items are stored as JSON in one column, so getting them back intact is not free.
        var stored = await context.TaskLists.FindAsync(created.LocalId);
        Assert.Equal("Buy milk", Assert.Single(stored!.Items).Description);
    }

    [Fact]
    public async Task An_entry_added_on_the_phone_takes_the_servers_id_and_keeps_it_across_later_saves()
    {
        // An entry created here has no server id until the push comes back with one. If a later save is
        // built from a copy read before that, it sends no id, the server mints a second entry, and
        // anything pointing at the first - a restock task, a daily reminder's record, an overdue notice -
        // is cut loose. What makes the screen able to avoid that is the store holding the id afterwards.
        using var context = new TaskContext();
        var created = await context.TaskLists.CreateAsync("Groceries", NewItems("Buy milk"));
        await context.SynchroniseAsync();

        var afterFirstSync = (await context.TaskLists.FindAsync(created.LocalId))!.Items;
        var entryId = Assert.Single(afterFirstSync).Id;
        Assert.NotEqual(Guid.Empty, entryId);

        await context.TaskLists.UpdateAsync(
            created.LocalId, "Groceries", [afterFirstSync[0] with { IsCompleted = true }]);
        await context.SynchroniseAsync();

        var onTheServer = Assert.Single(context.Server.TaskLists.Single(list => list.Title == "Groceries").Items);
        Assert.Equal(entryId, onTheServer.Id);
        Assert.True(onTheServer.IsCompleted);
    }

    /// <summary>A fresh entry as the screen builds one: no server id yet, which is Guid.Empty locally.</summary>
    private static IReadOnlyList<TaskItemDto> NewItems(string description)
        => [new(Guid.Empty, description, null, false, null, "None", false, "None", new TimeOnly(9, 0))];

    [Fact]
    public async Task A_list_created_and_then_edited_offline_arrives_in_the_order_it_happened()
    {
        using var context = new TaskContext();
        context.GoOffline();
        var taskList = await context.TaskLists.CreateAsync("Draft", SomeItems);
        await context.TaskLists.UpdateAsync(taskList.LocalId, "Finished", SomeItems);

        context.ComeBackOnline();
        await context.SynchroniseAsync();

        Assert.Equal("Finished", context.Server.TaskLists.Single().Title);
        Assert.Equal(
            ["POST /api/tasks", "PUT /api/tasks/" + context.Server.TaskLists.Single().Id],
            context.WriteRequests());
    }

    [Fact]
    public async Task Notes_and_task_lists_share_one_queue_without_being_sent_as_each_other()
    {
        using var context = new TaskContext();
        context.GoOffline();
        await context.Notes.CreateAsync("A note", [new NoteContentLineDto("text", false, false)]);
        await context.TaskLists.CreateAsync("A list", SomeItems);

        context.ComeBackOnline();
        var taskResult = await context.SynchroniseAsync();

        // One queue, one order - but each synchroniser only ever takes its own rows, and a note left
        // queued must not stop a task list going out.
        Assert.Equal(1, taskResult.Sent);
        Assert.Equal("A list", context.Server.TaskLists.Single().Title);
        Assert.Equal(1, await context.CountQueuedAsync(SyncEntityType.Note));
        Assert.Equal(0, await context.CountQueuedAsync(SyncEntityType.TaskList));
    }

    [Fact]
    public async Task A_list_written_elsewhere_appears_on_the_phone()
    {
        using var context = new TaskContext();
        context.Server.AddTaskList("Written elsewhere");

        var result = await context.SynchroniseAsync();

        Assert.Equal(1, result.Received);
        Assert.Equal("Written elsewhere", (await context.TaskLists.GetAllAsync()).Single().Title);
    }

    [Fact]
    public async Task A_list_deleted_elsewhere_leaves_the_phone_too()
    {
        using var context = new TaskContext();
        var remote = context.Server.AddTaskList("Doomed");
        await context.SynchroniseAsync();

        context.Clock.Advance(TimeSpan.FromMinutes(1));
        context.Server.DeleteTaskList(remote.Id);
        var result = await context.SynchroniseAsync();

        Assert.Equal(1, result.RemovedLocally);
        Assert.Empty(await context.TaskLists.GetAllAsync());
    }

    [Fact]
    public async Task An_unsent_local_edit_is_not_overwritten_by_the_servers_version()
    {
        using var context = new TaskContext();
        var taskList = await context.TaskLists.CreateAsync("Groceries", SomeItems);
        await context.SynchroniseAsync();

        context.GoOffline();
        await context.TaskLists.UpdateAsync(taskList.LocalId, "Edited on the phone", SomeItems);
        context.ComeBackOnline();
        context.Server.IsUnreachable = false;

        // The pull runs, but the queued edit has not gone out yet on the first pass.
        await context.SynchroniseAsync();

        Assert.Equal("Edited on the phone", (await context.TaskLists.GetAllAsync()).Single().Title);
    }

    [Fact]
    public async Task A_list_created_and_deleted_before_ever_syncing_is_never_sent_at_all()
    {
        using var context = new TaskContext();
        context.GoOffline();
        var taskList = await context.TaskLists.CreateAsync("Mistake", SomeItems);
        await context.TaskLists.DeleteAsync(taskList.LocalId);

        context.ComeBackOnline();
        await context.SynchroniseAsync();

        Assert.Empty(context.WriteRequests());
        Assert.Empty(context.Server.TaskLists);
    }

    [Fact]
    public async Task Two_runs_at_once_do_not_create_the_same_list_twice()
    {
        using var context = new TaskContext();
        context.GoOffline();
        await context.TaskLists.CreateAsync("Groceries", SomeItems);
        context.ComeBackOnline();

        // Both runs load the outbox into their own context, so without a gate both would find the row's
        // server id still null and both would send the create - two lists on the server out of one.
        var held = new TaskCompletionSource();
        context.Server.HoldRequestsUntil = held;
        var first = context.SynchroniseAsync();
        var second = context.SynchroniseAsync();
        held.SetResult();
        await Task.WhenAll(first, second);

        Assert.Single(context.Server.TaskLists);
        Assert.Single(context.WriteRequests());
    }

    [Fact]
    public async Task A_change_queued_while_a_sync_is_running_still_gets_sent()
    {
        using var context = new TaskContext();
        var held = new TaskCompletionSource();
        context.Server.HoldRequestsUntil = held;

        // A screen's own sync, started before the user's change exists.
        var inFlight = context.SynchroniseAsync();
        await context.TaskLists.CreateAsync("Added mid-sync", SomeItems);
        var afterTheChange = context.SynchroniseAsync();
        held.SetResult();
        await Task.WhenAll(inFlight, afterTheChange);

        // Dropping the second run - the first design - left this queued while the screen said "Synced",
        // because the run it deferred to had begun before the list existed.
        Assert.Contains(context.Server.TaskLists, list => list.Title == "Added mid-sync");
        Assert.Equal(0, await context.CountQueuedAsync(SyncEntityType.TaskList));
    }

    [Fact]
    public async Task A_queue_entry_that_outlived_the_create_it_describes_does_not_create_a_second_list()
    {
        using var context = new TaskContext();
        var taskList = await context.TaskLists.CreateAsync("Groceries", SomeItems);
        await context.SynchroniseAsync();

        // What a crash between "the server accepted this" and "the queue entry is gone" leaves behind.
        // The row's server id is saved first precisely so the replay that follows is a no-op; before
        // that ordering, this produced two lists on the server out of one.
        await context.RequeueCreateAsync(taskList.LocalId);
        await context.SynchroniseAsync();

        Assert.Single(context.Server.TaskLists);
    }

    [Fact]
    public async Task Syncing_offline_reports_it_rather_than_throwing()
    {
        using var context = new TaskContext();
        context.GoOffline();
        await context.TaskLists.CreateAsync("Groceries", SomeItems);

        var result = await context.SynchroniseAsync();

        Assert.False(result.ReachedTheServer);
        Assert.Equal(1, await context.CountQueuedAsync(SyncEntityType.TaskList));
    }

    [Fact]
    public async Task Pinned_lists_are_shown_first()
    {
        using var context = new TaskContext();
        var pinned = context.Server.AddTaskList("Pinned") with { IsPinned = true };
        context.Server.AddTaskList("Ordinary");
        context.Server.ReplaceForTest(pinned);

        await context.SynchroniseAsync();

        Assert.Equal("Pinned", (await context.TaskLists.GetAllAsync())[0].Title);
    }

    [Fact]
    public async Task Pinning_a_list_here_reaches_the_server()
    {
        using var context = new TaskContext();
        var taskList = await context.TaskLists.CreateAsync("Groceries", SomeItems);
        await context.SynchroniseAsync();

        await context.TaskLists.SetPinnedAsync(taskList.LocalId, true);
        await context.SynchroniseAsync();

        Assert.True(context.Server.TaskLists.Single().IsPinned);
        Assert.Equal(0, await context.CountQueuedAsync(SyncEntityType.TaskList));
    }

    [Fact]
    public async Task A_list_somebody_shared_cannot_be_pinned_here()
    {
        using var context = new TaskContext();
        context.Server.AddTaskList("Theirs", isShared: true);
        await context.SynchroniseAsync();
        var stored = Assert.Single(await context.TaskLists.GetAllAsync());

        var outcome = await context.TaskLists.SetPinnedAsync(stored.LocalId, true);

        Assert.Equal(LocalWriteOutcome.NotYours, outcome);
        Assert.False(context.Server.TaskLists.Single().IsPinned);
    }

    private sealed class TaskContext : IDisposable
    {
        private readonly LocalStore _localStore = new();

        public TaskContext()
        {
            Clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
            Server = new FakeTasksServer(Clock);
            TaskLists = new LocalTaskListRepository(_localStore, Clock, FixedNetworkStatus.Online);
            Notes = new LocalNoteRepository(_localStore, Clock, FixedNetworkStatus.Online);
            Synchronizer = new TaskListSynchronizer(
                _localStore, new TasksClient(Server.ToHttpClient()), Clock, new SyncGate(),
                NullLogger<TaskListSynchronizer>.Instance);
        }

        public FakeTimeProvider Clock { get; }
        public FakeTasksServer Server { get; }
        public LocalTaskListRepository TaskLists { get; }
        public LocalNoteRepository Notes { get; }
        public TaskListSynchronizer Synchronizer { get; }

        public Task<SyncResult> SynchroniseAsync() => Synchronizer.SynchroniseAsync(CancellationToken.None);

        public IReadOnlyList<string> WriteRequests()
            => Server.ReceivedRequests.Where(request => !request.Contains("/changes")).ToList();

        /// <summary>Puts back a create the server has already accepted - see the crash it stands in for.</summary>
        public async Task RequeueCreateAsync(Guid localId)
        {
            await using var dbContext = _localStore.CreateDbContext();
            dbContext.Outbox.Add(new OutboxEntry
            {
                EntityType = SyncEntityType.TaskList,
                LocalId = localId,
                Operation = OutboxOperation.Create,
                QueuedAtUtc = Clock.GetUtcNow()
            });

            await dbContext.SaveChangesAsync();
        }

        public async Task<int> CountQueuedAsync(string entityType)
        {
            await using var dbContext = _localStore.CreateDbContext();
            return await dbContext.Outbox.CountAsync(entry => entry.EntityType == entityType);
        }

        public void GoOffline() => Server.IsUnreachable = true;

        public void ComeBackOnline() => Server.IsUnreachable = false;

        public void Dispose()
        {
            Server.Dispose();
            _localStore.Dispose();
        }
    }
}
