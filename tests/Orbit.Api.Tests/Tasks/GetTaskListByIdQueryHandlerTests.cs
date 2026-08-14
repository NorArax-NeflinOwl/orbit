using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.GetTaskListById;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

public sealed class GetTaskListByIdQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_the_task_list_when_owned_by_the_requesting_user()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new GetTaskListByIdQueryHandler(repository);
        var userId = Guid.NewGuid();
        var taskList = TaskList.Create(userId, "Errands", []);
        await repository.AddAsync(taskList, CancellationToken.None);

        var result = await handler.HandleAsync(new GetTaskListByIdQuery(userId, taskList.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(taskList.Id, result!.Id);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_a_task_list_owned_by_a_different_user()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new GetTaskListByIdQueryHandler(repository);
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
        var handler = new GetTaskListByIdQueryHandler(new InMemoryTaskRepository());

        var result = await handler.HandleAsync(new GetTaskListByIdQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }
}
