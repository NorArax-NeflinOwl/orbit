using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.ShareTaskList;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

public sealed class ShareTaskListCommandHandlerTests
{
    private static ShareTaskListCommandHandler CreateHandler(InMemoryTaskRepository taskRepository, InMemoryTaskListShareRepository taskListShareRepository)
        => new(new TaskListAccessResolver(taskRepository, taskListShareRepository, new InMemoryUserRepository()), taskListShareRepository);

    [Fact]
    public async Task HandleAsync_creates_a_share_for_a_task_list_owned_by_the_requesting_user()
    {
        var taskRepository = new InMemoryTaskRepository();
        var shareRepository = new InMemoryTaskListShareRepository();
        var handler = CreateHandler(taskRepository, shareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var taskList = TaskList.Create(ownerId, "Errands", [TaskItem.Create("Buy milk", null, false)]);
        await taskRepository.AddAsync(taskList, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new ShareTaskListCommand(ownerId, taskList.Id, recipientId, ShareAccessLevel.CanEdit), CancellationToken.None);

        Assert.NotNull(outcome);
        Assert.False(outcome!.AlreadyShared);
        var share = await shareRepository.GetByIdAsync(recipientId, outcome.ShareId, CancellationToken.None);
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
        var handler = CreateHandler(taskRepository, new InMemoryTaskListShareRepository());
        var ownerId = Guid.NewGuid();
        var taskList = TaskList.Create(ownerId, "Errands", []);
        await taskRepository.AddAsync(taskList, CancellationToken.None);

        var outcome = await handler.HandleAsync(new ShareTaskListCommand(Guid.NewGuid(), taskList.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_unknown_task_list_id()
    {
        var handler = CreateHandler(new InMemoryTaskRepository(), new InMemoryTaskListShareRepository());

        var outcome = await handler.HandleAsync(new ShareTaskListCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_sharing_back_to_the_owner()
    {
        var taskRepository = new InMemoryTaskRepository();
        var handler = CreateHandler(taskRepository, new InMemoryTaskListShareRepository());
        var ownerId = Guid.NewGuid();
        var taskList = TaskList.Create(ownerId, "Errands", []);
        await taskRepository.AddAsync(taskList, CancellationToken.None);

        var outcome = await handler.HandleAsync(new ShareTaskListCommand(ownerId, taskList.Id, ownerId), CancellationToken.None);

        Assert.Null(outcome);
    }

    private static async Task<(Guid OwnerId, Guid RecipientId, TaskList TaskList)> ShareTaskListWithAcceptedGrantAsync(
        InMemoryTaskRepository taskRepository, InMemoryTaskListShareRepository taskListShareRepository, ShareAccessLevel accessLevel)
    {
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var taskList = TaskList.Create(ownerId, "Errands", []);
        await taskRepository.AddAsync(taskList, CancellationToken.None);
        var grant = TaskListShare.Create(taskList.Id, ownerId, recipientId, accessLevel);
        grant.MarkAccepted();
        await taskListShareRepository.AddAsync(grant, CancellationToken.None);
        return (ownerId, recipientId, taskList);
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_a_ReadOnly_recipient_tries_to_re_share()
    {
        var taskRepository = new InMemoryTaskRepository();
        var taskListShareRepository = new InMemoryTaskListShareRepository();
        var (_, recipientId, taskList) = await ShareTaskListWithAcceptedGrantAsync(taskRepository, taskListShareRepository, ShareAccessLevel.ReadOnly);
        var handler = CreateHandler(taskRepository, taskListShareRepository);

        var outcome = await handler.HandleAsync(new ShareTaskListCommand(recipientId, taskList.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task HandleAsync_lets_a_Share_tier_recipient_re_share_at_ReadOnly_or_Share_but_not_CanEdit()
    {
        var taskRepository = new InMemoryTaskRepository();
        var taskListShareRepository = new InMemoryTaskListShareRepository();
        var (_, recipientId, taskList) = await ShareTaskListWithAcceptedGrantAsync(taskRepository, taskListShareRepository, ShareAccessLevel.Share);
        var handler = CreateHandler(taskRepository, taskListShareRepository);

        var shareOutcome = await handler.HandleAsync(
            new ShareTaskListCommand(recipientId, taskList.Id, Guid.NewGuid(), ShareAccessLevel.Share), CancellationToken.None);
        var canEditOutcome = await handler.HandleAsync(
            new ShareTaskListCommand(recipientId, taskList.Id, Guid.NewGuid(), ShareAccessLevel.CanEdit), CancellationToken.None);

        Assert.NotNull(shareOutcome);
        Assert.Null(canEditOutcome);
    }

    [Fact]
    public async Task HandleAsync_reuses_an_existing_offer_to_the_same_recipient_instead_of_creating_a_duplicate()
    {
        var taskRepository = new InMemoryTaskRepository();
        var shareRepository = new InMemoryTaskListShareRepository();
        var handler = CreateHandler(taskRepository, shareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var taskList = TaskList.Create(ownerId, "Errands", []);
        await taskRepository.AddAsync(taskList, CancellationToken.None);

        var firstOutcome = await handler.HandleAsync(new ShareTaskListCommand(ownerId, taskList.Id, recipientId), CancellationToken.None);
        var secondOutcome = await handler.HandleAsync(new ShareTaskListCommand(ownerId, taskList.Id, recipientId), CancellationToken.None);

        Assert.NotNull(firstOutcome);
        Assert.False(firstOutcome!.AlreadyShared);
        Assert.NotNull(secondOutcome);
        Assert.True(secondOutcome!.AlreadyShared);
        Assert.Equal(firstOutcome.ShareId, secondOutcome.ShareId);
    }
}
