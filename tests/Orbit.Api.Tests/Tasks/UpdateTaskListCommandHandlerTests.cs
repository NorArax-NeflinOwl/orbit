using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.UpdateTaskList;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

public sealed class UpdateTaskListCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_updates_a_task_list_owned_by_the_requesting_user()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new UpdateTaskListCommandHandler(repository, new TaskListLinkValidator(repository));
        var userId = Guid.NewGuid();
        var taskList = TaskList.Create(userId, "Original title", [TaskItem.Create("Original item", null, false)]);
        await repository.AddAsync(taskList, CancellationToken.None);
        var newItems = new[] { TaskItem.Create("New item", null, false) };

        var wasUpdated = await handler.HandleAsync(
            new UpdateTaskListCommand(userId, taskList.Id, "New title", newItems), CancellationToken.None);

        Assert.True(wasUpdated);
        var stored = await repository.GetByIdAsync(userId, taskList.Id, CancellationToken.None);
        Assert.Equal("New title", stored!.Title);
        Assert.Equal("New item", Assert.Single(stored.Items).Description);
    }

    [Fact]
    public async Task HandleAsync_recomputes_completion_after_replacing_the_items()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new UpdateTaskListCommandHandler(repository, new TaskListLinkValidator(repository));
        var userId = Guid.NewGuid();
        var taskList = TaskList.Create(userId, "Errands", [TaskItem.Create("Buy milk", null, false)]);
        await repository.AddAsync(taskList, CancellationToken.None);
        var allDoneItems = new[] { TaskItem.Create("Buy milk", null, true) };

        await handler.HandleAsync(new UpdateTaskListCommand(userId, taskList.Id, "Errands", allDoneItems), CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, taskList.Id, CancellationToken.None);
        Assert.True(stored!.IsCompleted);
    }

    [Fact]
    public async Task HandleAsync_returns_false_and_does_not_update_a_task_list_owned_by_a_different_user()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new UpdateTaskListCommandHandler(repository, new TaskListLinkValidator(repository));
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var taskList = TaskList.Create(ownerId, "Original title", []);
        await repository.AddAsync(taskList, CancellationToken.None);

        var wasUpdated = await handler.HandleAsync(
            new UpdateTaskListCommand(otherUserId, taskList.Id, "Hijacked title", []), CancellationToken.None);

        Assert.False(wasUpdated);
        var stored = await repository.GetByIdAsync(ownerId, taskList.Id, CancellationToken.None);
        Assert.Equal("Original title", stored!.Title);
    }

    [Fact]
    public async Task HandleAsync_returns_false_for_an_unknown_task_list_id()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new UpdateTaskListCommandHandler(repository, new TaskListLinkValidator(repository));

        var wasUpdated = await handler.HandleAsync(
            new UpdateTaskListCommand(Guid.NewGuid(), Guid.NewGuid(), "Title", []), CancellationToken.None);

        Assert.False(wasUpdated);
    }

    [Fact]
    public async Task HandleAsync_returns_false_and_does_not_update_a_shared_read_only_task_list()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new UpdateTaskListCommandHandler(repository, new TaskListLinkValidator(repository));
        var recipientId = Guid.NewGuid();
        var sharedTaskList = TaskList.CreateShared(recipientId, "Original title", [], "owner", ShareAccessLevel.ReadOnly, Guid.NewGuid());
        await repository.AddAsync(sharedTaskList, CancellationToken.None);

        var wasUpdated = await handler.HandleAsync(
            new UpdateTaskListCommand(recipientId, sharedTaskList.Id, "Edited title", []), CancellationToken.None);

        Assert.False(wasUpdated);
        var stored = await repository.GetByIdAsync(recipientId, sharedTaskList.Id, CancellationToken.None);
        Assert.Equal("Original title", stored!.Title);
    }

    [Fact]
    public async Task HandleAsync_returns_false_and_does_not_update_a_task_list_shared_at_the_Share_tier()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new UpdateTaskListCommandHandler(repository, new TaskListLinkValidator(repository));
        var recipientId = Guid.NewGuid();
        var sharedTaskList = TaskList.CreateShared(recipientId, "Original title", [], "owner", ShareAccessLevel.Share, Guid.NewGuid());
        await repository.AddAsync(sharedTaskList, CancellationToken.None);

        var wasUpdated = await handler.HandleAsync(
            new UpdateTaskListCommand(recipientId, sharedTaskList.Id, "Edited title", []), CancellationToken.None);

        Assert.False(wasUpdated);
        var stored = await repository.GetByIdAsync(recipientId, sharedTaskList.Id, CancellationToken.None);
        Assert.Equal("Original title", stored!.Title);
    }

    [Fact]
    public async Task HandleAsync_updates_a_shared_task_list_with_edit_access()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new UpdateTaskListCommandHandler(repository, new TaskListLinkValidator(repository));
        var recipientId = Guid.NewGuid();
        var sharedTaskList = TaskList.CreateShared(recipientId, "Original title", [], "owner", ShareAccessLevel.CanEdit, Guid.NewGuid());
        await repository.AddAsync(sharedTaskList, CancellationToken.None);

        var wasUpdated = await handler.HandleAsync(
            new UpdateTaskListCommand(recipientId, sharedTaskList.Id, "New title", []), CancellationToken.None);

        Assert.True(wasUpdated);
        var stored = await repository.GetByIdAsync(recipientId, sharedTaskList.Id, CancellationToken.None);
        Assert.Equal("New title", stored!.Title);
    }

    [Fact]
    public async Task HandleAsync_rejects_an_update_that_links_an_item_to_the_list_itself()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new UpdateTaskListCommandHandler(repository, new TaskListLinkValidator(repository));
        var userId = Guid.NewGuid();
        var taskList = TaskList.Create(userId, "Errands", []);
        await repository.AddAsync(taskList, CancellationToken.None);
        var itemsLinkingToSelf = new[] { TaskItem.Create("Self reference", null, false, taskList.Id) };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(
            new UpdateTaskListCommand(userId, taskList.Id, "Errands", itemsLinkingToSelf), CancellationToken.None));
    }
}
