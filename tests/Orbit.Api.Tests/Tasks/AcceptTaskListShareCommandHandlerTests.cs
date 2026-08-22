using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.AcceptTaskListShare;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

public sealed class AcceptTaskListShareCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_creates_a_copy_in_the_recipients_task_lists()
    {
        var taskRepository = new InMemoryTaskRepository();
        var shareRepository = new InMemoryTaskListShareRepository();
        var userRepository = new InMemoryUserRepository();
        var handler = new AcceptTaskListShareCommandHandler(shareRepository, taskRepository, userRepository);

        var owner = User.Create("owner@example.com", "owner", "Owner", "hash");
        await userRepository.AddAsync(owner, CancellationToken.None);
        var recipientId = Guid.NewGuid();
        var sourceTaskList = TaskList.Create(owner.Id, "Errands", [TaskItem.Create("Buy milk", null, true)]);
        await taskRepository.AddAsync(sourceTaskList, CancellationToken.None);
        var share = TaskListShare.Create(sourceTaskList.Id, owner.Id, recipientId, ShareAccessLevel.CanEdit);
        await shareRepository.AddAsync(share, CancellationToken.None);

        var accepted = await handler.HandleAsync(new AcceptTaskListShareCommand(recipientId, share.Id), CancellationToken.None);

        Assert.True(accepted);
        var recipientTaskLists = await taskRepository.GetAllAsync(recipientId, CancellationToken.None);
        var sharedCopy = Assert.Single(recipientTaskLists);
        Assert.True(sharedCopy.IsShared);
        Assert.Equal("owner", sharedCopy.SharedByUserName);
        Assert.Equal(ShareAccessLevel.CanEdit, sharedCopy.AccessLevel);
        Assert.Equal("Errands", sharedCopy.Title);
        var copiedItem = Assert.Single(sharedCopy.Items);
        Assert.Equal("Buy milk", copiedItem.Description);
        Assert.True(copiedItem.IsCompleted);
    }

    /// <summary>
    /// A linked item's LinkedTaskListId points into the owner's own other task lists - meaningless (and
    /// potentially pointing at something the recipient can't see) once copied into the recipient's own
    /// task lists, so TaskList.CreateShared strips it. See its class comment.
    /// </summary>
    [Fact]
    public async Task HandleAsync_strips_linked_task_list_ids_from_the_copied_items()
    {
        var taskRepository = new InMemoryTaskRepository();
        var shareRepository = new InMemoryTaskListShareRepository();
        var userRepository = new InMemoryUserRepository();
        var handler = new AcceptTaskListShareCommandHandler(shareRepository, taskRepository, userRepository);

        var owner = User.Create("owner@example.com", "owner", "Owner", "hash");
        await userRepository.AddAsync(owner, CancellationToken.None);
        var recipientId = Guid.NewGuid();
        var otherOwnedList = TaskList.Create(owner.Id, "Other list", []);
        var sourceTaskList = TaskList.Create(
            owner.Id, "Main list", [TaskItem.Create("Linked item", null, false, otherOwnedList.Id)]);
        await taskRepository.AddAsync(sourceTaskList, CancellationToken.None);
        var share = TaskListShare.Create(sourceTaskList.Id, owner.Id, recipientId);
        await shareRepository.AddAsync(share, CancellationToken.None);

        await handler.HandleAsync(new AcceptTaskListShareCommand(recipientId, share.Id), CancellationToken.None);

        var recipientTaskLists = await taskRepository.GetAllAsync(recipientId, CancellationToken.None);
        var sharedCopy = Assert.Single(recipientTaskLists);
        var copiedItem = Assert.Single(sharedCopy.Items);
        Assert.Null(copiedItem.LinkedTaskListId);
    }

    [Fact]
    public async Task HandleAsync_returns_false_when_the_share_was_not_offered_to_the_requesting_user()
    {
        var taskRepository = new InMemoryTaskRepository();
        var shareRepository = new InMemoryTaskListShareRepository();
        var userRepository = new InMemoryUserRepository();
        var handler = new AcceptTaskListShareCommandHandler(shareRepository, taskRepository, userRepository);

        var owner = User.Create("owner@example.com", "owner", "Owner", "hash");
        await userRepository.AddAsync(owner, CancellationToken.None);
        var sourceTaskList = TaskList.Create(owner.Id, "Errands", []);
        await taskRepository.AddAsync(sourceTaskList, CancellationToken.None);
        var share = TaskListShare.Create(sourceTaskList.Id, owner.Id, Guid.NewGuid());
        await shareRepository.AddAsync(share, CancellationToken.None);

        var accepted = await handler.HandleAsync(
            new AcceptTaskListShareCommand(Guid.NewGuid(), share.Id), CancellationToken.None);

        Assert.False(accepted);
    }

    [Fact]
    public async Task HandleAsync_returns_false_for_an_unknown_share_id()
    {
        var handler = new AcceptTaskListShareCommandHandler(
            new InMemoryTaskListShareRepository(), new InMemoryTaskRepository(), new InMemoryUserRepository());

        var accepted = await handler.HandleAsync(
            new AcceptTaskListShareCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(accepted);
    }

    [Fact]
    public async Task HandleAsync_is_idempotent_and_does_not_create_a_second_copy_when_accepted_twice()
    {
        var taskRepository = new InMemoryTaskRepository();
        var shareRepository = new InMemoryTaskListShareRepository();
        var userRepository = new InMemoryUserRepository();
        var handler = new AcceptTaskListShareCommandHandler(shareRepository, taskRepository, userRepository);

        var owner = User.Create("owner@example.com", "owner", "Owner", "hash");
        await userRepository.AddAsync(owner, CancellationToken.None);
        var recipientId = Guid.NewGuid();
        var sourceTaskList = TaskList.Create(owner.Id, "Errands", []);
        await taskRepository.AddAsync(sourceTaskList, CancellationToken.None);
        var share = TaskListShare.Create(sourceTaskList.Id, owner.Id, recipientId);
        await shareRepository.AddAsync(share, CancellationToken.None);

        var command = new AcceptTaskListShareCommand(recipientId, share.Id);
        await handler.HandleAsync(command, CancellationToken.None);
        var acceptedAgain = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(acceptedAgain);
        var recipientTaskLists = await taskRepository.GetAllAsync(recipientId, CancellationToken.None);
        Assert.Single(recipientTaskLists);
    }
}
