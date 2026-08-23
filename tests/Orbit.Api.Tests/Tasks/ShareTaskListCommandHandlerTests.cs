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
        Assert.Equal(ownerId, share.OriginalOwnerUserId);
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

        var outcome = await handler.HandleAsync(
            new ShareTaskListCommand(Guid.NewGuid(), taskList.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_unknown_task_list_id()
    {
        var handler = new ShareTaskListCommandHandler(new InMemoryTaskRepository(), new InMemoryTaskListShareRepository());

        var outcome = await handler.HandleAsync(
            new ShareTaskListCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_sharing_back_to_the_original_owner()
    {
        var taskRepository = new InMemoryTaskRepository();
        var handler = new ShareTaskListCommandHandler(taskRepository, new InMemoryTaskListShareRepository());
        var ownerId = Guid.NewGuid();
        var taskList = TaskList.Create(ownerId, "Errands", []);
        await taskRepository.AddAsync(taskList, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new ShareTaskListCommand(ownerId, taskList.Id, ownerId), CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_a_ReadOnly_recipient_tries_to_re_share()
    {
        var taskRepository = new InMemoryTaskRepository();
        var handler = new ShareTaskListCommandHandler(taskRepository, new InMemoryTaskListShareRepository());
        var originalOwnerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var sharedCopy = TaskList.CreateShared(recipientId, "Errands", [], "owner", ShareAccessLevel.ReadOnly, originalOwnerId);
        await taskRepository.AddAsync(sharedCopy, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new ShareTaskListCommand(recipientId, sharedCopy.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task HandleAsync_lets_a_Share_tier_recipient_re_share_at_ReadOnly_or_Share_but_not_CanEdit()
    {
        var taskRepository = new InMemoryTaskRepository();
        var handler = new ShareTaskListCommandHandler(taskRepository, new InMemoryTaskListShareRepository());
        var originalOwnerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var sharedCopy = TaskList.CreateShared(recipientId, "Errands", [], "owner", ShareAccessLevel.Share, originalOwnerId);
        await taskRepository.AddAsync(sharedCopy, CancellationToken.None);

        var shareOutcome = await handler.HandleAsync(
            new ShareTaskListCommand(recipientId, sharedCopy.Id, Guid.NewGuid(), ShareAccessLevel.Share), CancellationToken.None);
        var canEditOutcome = await handler.HandleAsync(
            new ShareTaskListCommand(recipientId, sharedCopy.Id, Guid.NewGuid(), ShareAccessLevel.CanEdit), CancellationToken.None);

        Assert.NotNull(shareOutcome);
        Assert.Null(canEditOutcome);
    }

    [Fact]
    public async Task HandleAsync_reuses_an_existing_offer_to_the_same_recipient_instead_of_creating_a_duplicate()
    {
        var taskRepository = new InMemoryTaskRepository();
        var shareRepository = new InMemoryTaskListShareRepository();
        var handler = new ShareTaskListCommandHandler(taskRepository, shareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var taskList = TaskList.Create(ownerId, "Errands", []);
        await taskRepository.AddAsync(taskList, CancellationToken.None);

        var firstOutcome = await handler.HandleAsync(
            new ShareTaskListCommand(ownerId, taskList.Id, recipientId), CancellationToken.None);
        var secondOutcome = await handler.HandleAsync(
            new ShareTaskListCommand(ownerId, taskList.Id, recipientId), CancellationToken.None);

        Assert.NotNull(firstOutcome);
        Assert.False(firstOutcome!.AlreadyShared);
        Assert.NotNull(secondOutcome);
        Assert.True(secondOutcome!.AlreadyShared);
        Assert.Equal(firstOutcome.ShareId, secondOutcome.ShareId);
    }
}
