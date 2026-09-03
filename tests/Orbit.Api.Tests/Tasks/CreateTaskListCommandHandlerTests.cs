using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.CreateTaskList;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

public sealed class CreateTaskListCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_creates_a_task_list_owned_by_the_requesting_user()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new CreateTaskListCommandHandler(repository, new TaskListLinkValidator(repository));
        var userId = Guid.NewGuid();
        var items = new[] { TaskItem.Create("Buy milk", dueDateUtc: null, isCompleted: false) };

        var taskListId = await handler.HandleAsync(new CreateTaskListCommand(userId, "Errands", items, IsGroup: false, IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, taskListId, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("Errands", stored!.Title);
        Assert.Equal("Buy milk", Assert.Single(stored.Items).Description);
    }

    [Fact]
    public async Task HandleAsync_marks_the_task_list_completed_only_when_every_item_is_checked_off()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new CreateTaskListCommandHandler(repository, new TaskListLinkValidator(repository));
        var userId = Guid.NewGuid();
        var items = new[]
        {
            TaskItem.Create("Buy milk", dueDateUtc: null, isCompleted: true),
            TaskItem.Create("Buy eggs", dueDateUtc: null, isCompleted: false)
        };

        var taskListId = await handler.HandleAsync(new CreateTaskListCommand(userId, "Errands", items, IsGroup: false, IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, taskListId, CancellationToken.None);
        Assert.False(stored!.IsCompleted);
    }

    [Fact]
    public async Task HandleAsync_creates_an_item_linked_to_another_task_list()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        var linkedList = TaskList.Create(userId, "Linked list", []);
        await repository.AddAsync(linkedList, CancellationToken.None);
        var handler = new CreateTaskListCommandHandler(repository, new TaskListLinkValidator(repository));
        var items = new[] { TaskItem.Create("Depends on linked list", null, false, [linkedList.Id]) };

        var taskListId = await handler.HandleAsync(new CreateTaskListCommand(userId, "Main list", items, IsGroup: false, IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, taskListId, CancellationToken.None);
        Assert.Equal([linkedList.Id], Assert.Single(stored!.Items).LinkedTaskListIds);
    }

    [Fact]
    public async Task HandleAsync_ignores_a_requested_completion_for_a_linked_item()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        var linkedList = TaskList.Create(userId, "Linked list", []);
        await repository.AddAsync(linkedList, CancellationToken.None);
        var handler = new CreateTaskListCommandHandler(repository, new TaskListLinkValidator(repository));
        // A linked item's completion can't be set manually - see TaskItem.Create - so this is expected
        // to be stored as not completed even though the request asked for isCompleted: true.
        var items = new[] { TaskItem.Create("Depends on linked list", null, isCompleted: true, [linkedList.Id]) };

        var taskListId = await handler.HandleAsync(new CreateTaskListCommand(userId, "Main list", items, IsGroup: false, IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, taskListId, CancellationToken.None);
        Assert.False(Assert.Single(stored!.Items).IsCompleted);
    }

    [Fact]
    public async Task HandleAsync_rejects_an_item_linked_to_an_unknown_task_list()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new CreateTaskListCommandHandler(repository, new TaskListLinkValidator(repository));
        var items = new[] { TaskItem.Create("Depends on nothing", null, false, [Guid.NewGuid()]) };

        await Assert.ThrowsAsync<InvalidRequestException>(
            () => handler.HandleAsync(new CreateTaskListCommand(Guid.NewGuid(), "Main list", items, IsGroup: false, IsPrivate: false, EncryptedContent: null), CancellationToken.None));
    }
}
