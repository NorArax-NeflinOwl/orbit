using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.AcquireTaskListLock;
using Orbit.Core.Tasks.ReleaseTaskListLock;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// Holding a list open is not a change to it. Both ends of that: the lock is saved on its own rather
/// than through the path that replaces every entry (which is where the duplicate-key failures on /lock
/// came from - see ITaskRepository.UpdateLockAsync), and what is on the list survives untouched.
/// </summary>
public sealed class TaskListLockTests
{
    [Fact]
    public async Task Taking_a_lock_writes_the_lock_and_not_the_list()
    {
        var context = new LockTestContext();
        var taskList = await context.AListWithEntriesAsync();

        var outcome = await context.AcquireAsync(taskList.Id);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        Assert.Equal(1, context.Repository.LockSaves);
        Assert.Equal(context.OwnerId, taskList.LockedByUserId);
        Assert.Equal(["Buy milk", "Buy bread"], taskList.Items.Select(item => item.Description));
    }

    /// <summary>
    /// The heartbeat behind an open editor takes the lock again every twenty seconds. Each one used to
    /// delete and re-insert every entry on the list, its links and its categories with them.
    /// </summary>
    [Fact]
    public async Task A_heartbeat_costs_one_lock_write_each_time_and_nothing_else()
    {
        var context = new LockTestContext();
        var taskList = await context.AListWithEntriesAsync();

        await context.AcquireAsync(taskList.Id);
        await context.AcquireAsync(taskList.Id);
        await context.AcquireAsync(taskList.Id);

        Assert.Equal(3, context.Repository.LockSaves);
        Assert.Equal(2, taskList.Items.Count);
    }

    [Fact]
    public async Task Letting_go_writes_the_lock_on_its_own_too()
    {
        var context = new LockTestContext();
        var taskList = await context.AListWithEntriesAsync();
        await context.AcquireAsync(taskList.Id);

        Assert.True(await context.ReleaseAsync(taskList.Id));

        Assert.Equal(2, context.Repository.LockSaves);
        Assert.Null(taskList.LockedByUserId);
    }

    [Fact]
    public async Task A_list_somebody_else_is_holding_is_not_taken_and_nothing_is_written()
    {
        var context = new LockTestContext();
        var taskList = await context.AListWithEntriesAsync();
        taskList.AcquireLock(Guid.NewGuid(), "somebody-else", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        var outcome = await context.AcquireAsync(taskList.Id);

        Assert.Equal(EditOutcomeKind.Locked, outcome.Kind);
        Assert.Equal(0, context.Repository.LockSaves);
    }

    private sealed class LockTestContext
    {
        private readonly InMemoryUserRepository _userRepository = new();
        private readonly InMemoryTaskListShareRepository _shareRepository = new();

        public InMemoryTaskRepository Repository { get; } = new();
        public Guid OwnerId { get; }

        public LockTestContext()
        {
            var owner = User.Create("owner@example.com", "owner", "Owner", "hash");
            OwnerId = owner.Id;
            _userRepository.AddAsync(owner, CancellationToken.None).GetAwaiter().GetResult();
        }

        public async Task<TaskList> AListWithEntriesAsync()
        {
            var taskList = TaskList.Create(
                OwnerId, "Errands",
                [TaskItem.Create("Buy milk", null, false, categories: ["shopping"]), TaskItem.Create("Buy bread", null, false)]);
            await Repository.AddAsync(taskList, CancellationToken.None);
            return taskList;
        }

        public Task<EditOutcome> AcquireAsync(Guid taskListId)
            => new AcquireTaskListLockCommandHandler(Resolver, Repository, _userRepository)
                .HandleAsync(new AcquireTaskListLockCommand(OwnerId, taskListId), CancellationToken.None);

        public Task<bool> ReleaseAsync(Guid taskListId)
            => new ReleaseTaskListLockCommandHandler(Resolver, Repository)
                .HandleAsync(new ReleaseTaskListLockCommand(OwnerId, taskListId), CancellationToken.None);

        private TaskListAccessResolver Resolver => new(Repository, _shareRepository, _userRepository);
    }
}
