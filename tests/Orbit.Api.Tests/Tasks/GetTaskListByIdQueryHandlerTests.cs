using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.GetTaskListById;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

public sealed class GetTaskListByIdQueryHandlerTests
{
    private static GetTaskListByIdQueryHandler CreateHandler(InMemoryTaskRepository taskRepository, InMemoryTaskListShareRepository? taskListShareRepository = null)
        => new(
            new TaskListAccessResolver(taskRepository, taskListShareRepository ?? new InMemoryTaskListShareRepository(), new InMemoryUserRepository()),
            new LinkedTaskCompletionResolver());

    [Fact]
    public async Task HandleAsync_returns_the_task_list_when_owned_by_the_requesting_user()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
        var userId = Guid.NewGuid();
        var taskList = TaskList.Create(userId, "Errands", []);
        await repository.AddAsync(taskList, CancellationToken.None);

        var result = await handler.HandleAsync(new GetTaskListByIdQuery(userId, taskList.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(taskList.Id, result!.Id);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_a_task_list_neither_owned_by_nor_shared_with_the_requesting_user()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var taskList = TaskList.Create(ownerId, "Errands", []);
        await repository.AddAsync(taskList, CancellationToken.None);

        var result = await handler.HandleAsync(new GetTaskListByIdQuery(otherUserId, taskList.Id), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_unknown_task_list_id()
    {
        var handler = CreateHandler(new InMemoryTaskRepository());

        var result = await handler.HandleAsync(new GetTaskListByIdQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_resolves_a_linked_items_completion_from_the_list_it_links_to()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        var linkedList = TaskList.Create(userId, "Linked list", [TaskItem.Create("Done", null, true)]);
        await repository.AddAsync(linkedList, CancellationToken.None);
        var mainList = TaskList.Create(userId, "Main list", [TaskItem.Create("Depends on linked list", null, false, linkedList.Id)]);
        await repository.AddAsync(mainList, CancellationToken.None);
        var handler = CreateHandler(repository);

        var result = await handler.HandleAsync(new GetTaskListByIdQuery(userId, mainList.Id), CancellationToken.None);

        Assert.True(result!.IsCompleted);
        Assert.True(Assert.Single(result.Items).IsCompleted);
    }

    [Fact]
    public async Task HandleAsync_returns_the_task_list_with_access_context_when_shared_via_an_accepted_grant()
    {
        var taskRepository = new InMemoryTaskRepository();
        var taskListShareRepository = new InMemoryTaskListShareRepository();
        var handler = CreateHandler(taskRepository, taskListShareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var taskList = TaskList.Create(ownerId, "Errands", []);
        await taskRepository.AddAsync(taskList, CancellationToken.None);
        var share = TaskListShare.Create(taskList.Id, ownerId, recipientId);
        share.MarkAccepted();
        await taskListShareRepository.AddAsync(share, CancellationToken.None);

        var result = await handler.HandleAsync(new GetTaskListByIdQuery(recipientId, taskList.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsShared);
    }
}
