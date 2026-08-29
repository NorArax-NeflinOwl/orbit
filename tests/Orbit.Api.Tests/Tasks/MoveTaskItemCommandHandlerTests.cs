using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.MoveTaskItem;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

public sealed class MoveTaskItemCommandHandlerTests
{
    private static MoveTaskItemCommandHandler CreateHandler(InMemoryTaskRepository taskRepository, InMemoryTaskListShareRepository? taskListShareRepository = null)
        => new(
            new TaskListAccessResolver(taskRepository, taskListShareRepository ?? new InMemoryTaskListShareRepository(), new InMemoryUserRepository()),
            taskRepository,
            new TaskListLinkValidator(taskRepository));

    [Fact]
    public async Task HandleAsync_moves_the_item_out_of_the_source_list_and_into_the_target_list()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var item = TaskItem.Create("Buy milk", null, false);
        var sourceList = TaskList.Create(userId, "Errands", [item]);
        var targetList = TaskList.Create(userId, "Groceries", []);
        await repository.AddAsync(sourceList, CancellationToken.None);
        await repository.AddAsync(targetList, CancellationToken.None);

        var outcome = await handler.HandleAsync(new MoveTaskItemCommand(userId, sourceList.Id, item.Id, targetList.Id), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        var storedSource = await repository.GetByIdAsync(userId, sourceList.Id, CancellationToken.None);
        var storedTarget = await repository.GetByIdAsync(userId, targetList.Id, CancellationToken.None);
        Assert.Empty(storedSource!.Items);
        Assert.Equal("Buy milk", Assert.Single(storedTarget!.Items).Description);
    }

    [Fact]
    public async Task HandleAsync_leaves_both_lists_as_important_as_they_were()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var item = TaskItem.Create("Buy milk", null, false);
        var sourceList = TaskList.Create(userId, "Errands", [item], priority: ItemPriority.High);
        var targetList = TaskList.Create(userId, "Groceries", [], priority: ItemPriority.Low);
        await repository.AddAsync(sourceList, CancellationToken.None);
        await repository.AddAsync(targetList, CancellationToken.None);

        await handler.HandleAsync(new MoveTaskItemCommand(userId, sourceList.Id, item.Id, targetList.Id), CancellationToken.None);

        // Moving one entry says nothing about how much either list matters - it used to reset both to
        // Normal, because TaskList.Update took the priority as an optional parameter and this left it out.
        var storedSource = await repository.GetByIdAsync(userId, sourceList.Id, CancellationToken.None);
        var storedTarget = await repository.GetByIdAsync(userId, targetList.Id, CancellationToken.None);
        Assert.Equal(ItemPriority.High, storedSource!.Priority);
        Assert.Equal(ItemPriority.Low, storedTarget!.Priority);
    }

    [Fact]
    public async Task HandleAsync_returns_NotFound_when_the_item_does_not_exist_in_the_source_list()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var sourceList = TaskList.Create(userId, "Errands", []);
        var targetList = TaskList.Create(userId, "Groceries", []);
        await repository.AddAsync(sourceList, CancellationToken.None);
        await repository.AddAsync(targetList, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new MoveTaskItemCommand(userId, sourceList.Id, Guid.NewGuid(), targetList.Id), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.NotFound, outcome.Kind);
    }

    [Fact]
    public async Task HandleAsync_returns_NotFound_for_an_unknown_target_list()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var item = TaskItem.Create("Buy milk", null, false);
        var sourceList = TaskList.Create(userId, "Errands", [item]);
        await repository.AddAsync(sourceList, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new MoveTaskItemCommand(userId, sourceList.Id, item.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.NotFound, outcome.Kind);
        var storedSource = await repository.GetByIdAsync(userId, sourceList.Id, CancellationToken.None);
        Assert.Single(storedSource!.Items);
    }

    [Fact]
    public async Task HandleAsync_returns_NotFound_when_the_target_list_belongs_to_a_different_owner()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var item = TaskItem.Create("Buy milk", null, false);
        var sourceList = TaskList.Create(userId, "Errands", [item]);
        var otherUsersList = TaskList.Create(otherUserId, "Not yours", []);
        await repository.AddAsync(sourceList, CancellationToken.None);
        await repository.AddAsync(otherUsersList, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new MoveTaskItemCommand(userId, sourceList.Id, item.Id, otherUsersList.Id), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.NotFound, outcome.Kind);
    }

    [Fact]
    public async Task HandleAsync_returns_NotFound_when_source_and_target_are_the_same_list()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var item = TaskItem.Create("Buy milk", null, false);
        var taskList = TaskList.Create(userId, "Errands", [item]);
        await repository.AddAsync(taskList, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new MoveTaskItemCommand(userId, taskList.Id, item.Id, taskList.Id), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.NotFound, outcome.Kind);
    }

    [Fact]
    public async Task HandleAsync_returns_Locked_when_the_target_list_is_locked_by_someone_else()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var item = TaskItem.Create("Buy milk", null, false);
        var sourceList = TaskList.Create(userId, "Errands", [item]);
        var targetList = TaskList.Create(userId, "Groceries", []);
        targetList.AcquireLock(otherUserId, "otherUser", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        await repository.AddAsync(sourceList, CancellationToken.None);
        await repository.AddAsync(targetList, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new MoveTaskItemCommand(userId, sourceList.Id, item.Id, targetList.Id), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Locked, outcome.Kind);
        Assert.Equal("otherUser", outcome.LockedByUserName);
    }

    [Fact]
    public async Task HandleAsync_leaves_the_grouping_flag_alone_on_both_lists()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var item = TaskItem.Create("Buy milk", null, false);
        var sourceList = TaskList.Create(userId, "Errands", [item], isGroup: true);
        var targetList = TaskList.Create(userId, "Groceries", [], isGroup: true);
        await repository.AddAsync(sourceList, CancellationToken.None);
        await repository.AddAsync(targetList, CancellationToken.None);

        await handler.HandleAsync(new MoveTaskItemCommand(userId, sourceList.Id, item.Id, targetList.Id), CancellationToken.None);

        // Moving an item replaces both lists' checklists wholesale (see TaskList.Update), which is
        // exactly where a flag that isn't carried along would quietly get dropped.
        Assert.True((await repository.GetByIdAsync(userId, sourceList.Id, CancellationToken.None))!.IsGroup);
        Assert.True((await repository.GetByIdAsync(userId, targetList.Id, CancellationToken.None))!.IsGroup);
    }
}
