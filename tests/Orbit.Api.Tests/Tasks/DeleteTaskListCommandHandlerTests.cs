using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.DeleteTaskList;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

public sealed class DeleteTaskListCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_deletes_a_task_list_owned_by_the_requesting_user()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new DeleteTaskListCommandHandler(repository, new InMemoryTaskListShareRepository(), new InMemorySyncTombstoneRepository());
        var userId = Guid.NewGuid();
        var taskList = TaskList.Create(userId, "Errands", []);
        await repository.AddAsync(taskList, CancellationToken.None);

        var wasDeleted = await handler.HandleAsync(new DeleteTaskListCommand(userId, taskList.Id), CancellationToken.None);

        Assert.True(wasDeleted);
        Assert.Null(await repository.GetByIdAsync(userId, taskList.Id, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_returns_false_and_does_not_delete_a_task_list_owned_by_a_different_user()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new DeleteTaskListCommandHandler(repository, new InMemoryTaskListShareRepository(), new InMemorySyncTombstoneRepository());
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var taskList = TaskList.Create(ownerId, "Errands", []);
        await repository.AddAsync(taskList, CancellationToken.None);

        var wasDeleted = await handler.HandleAsync(new DeleteTaskListCommand(otherUserId, taskList.Id), CancellationToken.None);

        Assert.False(wasDeleted);
        Assert.NotNull(await repository.GetByIdAsync(ownerId, taskList.Id, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_returns_false_for_an_unknown_task_list_id()
    {
        var handler = new DeleteTaskListCommandHandler(new InMemoryTaskRepository(), new InMemoryTaskListShareRepository(), new InMemorySyncTombstoneRepository());

        var wasDeleted = await handler.HandleAsync(new DeleteTaskListCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(wasDeleted);
    }
}
