using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.ShareTaskList;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

public sealed class ShareTaskListCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_creates_a_share_for_a_task_list_owned_by_the_requesting_user()
    {
        var taskRepository = new InMemoryTaskRepository();
        var shareRepository = new InMemoryTaskListShareRepository();
        var handler = new ShareTaskListCommandHandler(taskRepository, shareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var taskList = TaskList.Create(ownerId, "Errands", [TaskItem.Create("Buy milk", null, false)]);
        await taskRepository.AddAsync(taskList, CancellationToken.None);

        var shareId = await handler.HandleAsync(
            new ShareTaskListCommand(ownerId, taskList.Id, recipientId, ShareAccessLevel.CanEdit), CancellationToken.None);

        Assert.NotNull(shareId);
        var share = await shareRepository.GetByIdAsync(recipientId, shareId!.Value, CancellationToken.None);
        Assert.NotNull(share);
        Assert.Equal(taskList.Id, share!.SourceTaskListId);
        Assert.Equal(ownerId, share.OwnerUserId);
        Assert.Equal(recipientId, share.RecipientUserId);
        Assert.Equal(ShareAccessLevel.CanEdit, share.AccessLevel);
        Assert.False(share.IsAccepted);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_a_task_list_not_owned_by_the_requesting_user()
    {
        var taskRepository = new InMemoryTaskRepository();
        var handler = new ShareTaskListCommandHandler(taskRepository, new InMemoryTaskListShareRepository());
        var ownerId = Guid.NewGuid();
        var taskList = TaskList.Create(ownerId, "Errands", []);
        await taskRepository.AddAsync(taskList, CancellationToken.None);

        var shareId = await handler.HandleAsync(
            new ShareTaskListCommand(Guid.NewGuid(), taskList.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Null(shareId);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_unknown_task_list_id()
    {
        var handler = new ShareTaskListCommandHandler(new InMemoryTaskRepository(), new InMemoryTaskListShareRepository());

        var shareId = await handler.HandleAsync(
            new ShareTaskListCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(shareId);
    }
}
