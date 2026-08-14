using Orbit.Api.Tests.TestDoubles;
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
        var handler = new CreateTaskListCommandHandler(repository);
        var userId = Guid.NewGuid();
        var items = new[] { TaskItem.Create("Buy milk", dueDateUtc: null, isCompleted: false) };

        var taskListId = await handler.HandleAsync(new CreateTaskListCommand(userId, "Errands", items), CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, taskListId, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("Errands", stored!.Title);
        Assert.Equal("Buy milk", Assert.Single(stored.Items).Description);
    }

    [Fact]
    public async Task HandleAsync_marks_the_task_list_completed_only_when_every_item_is_checked_off()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new CreateTaskListCommandHandler(repository);
        var userId = Guid.NewGuid();
        var items = new[]
        {
            TaskItem.Create("Buy milk", dueDateUtc: null, isCompleted: true),
            TaskItem.Create("Buy eggs", dueDateUtc: null, isCompleted: false)
        };

        var taskListId = await handler.HandleAsync(new CreateTaskListCommand(userId, "Errands", items), CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, taskListId, CancellationToken.None);
        Assert.False(stored!.IsCompleted);
    }
}
