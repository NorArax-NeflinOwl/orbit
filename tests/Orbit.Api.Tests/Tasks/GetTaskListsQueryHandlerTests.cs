using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.GetTaskLists;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

public sealed class GetTaskListsQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_only_task_lists_owned_by_the_requesting_user()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new GetTaskListsQueryHandler(repository);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await repository.AddAsync(TaskList.Create(userId, "Mine", []), CancellationToken.None);
        await repository.AddAsync(TaskList.Create(otherUserId, "Not mine", []), CancellationToken.None);

        var taskLists = await handler.HandleAsync(new GetTaskListsQuery(userId), CancellationToken.None);

        var taskList = Assert.Single(taskLists);
        Assert.Equal("Mine", taskList.Title);
    }

    [Fact]
    public async Task HandleAsync_returns_an_empty_list_when_the_user_has_no_task_lists()
    {
        var handler = new GetTaskListsQueryHandler(new InMemoryTaskRepository());

        var taskLists = await handler.HandleAsync(new GetTaskListsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(taskLists);
    }
}
