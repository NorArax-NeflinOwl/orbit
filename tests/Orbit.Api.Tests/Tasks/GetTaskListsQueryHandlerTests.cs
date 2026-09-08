using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.GetTaskLists;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

public sealed class GetTaskListsQueryHandlerTests
{
    private static GetTaskListsQueryHandler CreateHandler(InMemoryTaskRepository taskRepository, InMemoryTaskListShareRepository? taskListShareRepository = null)
        => new(
            new TaskListAccessResolver(taskRepository, taskListShareRepository ?? new InMemoryTaskListShareRepository(), new InMemoryUserRepository()),
            new LinkedTaskCompletionResolver());

    [Fact]
    public async Task HandleAsync_returns_only_task_lists_owned_by_the_requesting_user()
    {
        var repository = new InMemoryTaskRepository();
        var handler = CreateHandler(repository);
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
        var handler = CreateHandler(new InMemoryTaskRepository());

        var taskLists = await handler.HandleAsync(new GetTaskListsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(taskLists);
    }

    [Fact]
    public async Task HandleAsync_resolves_a_linked_items_completion_from_the_list_it_links_to()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        var linkedList = TaskList.Create(userId, "Linked list", [TaskItem.Create("Done", null, true)]);
        await repository.AddAsync(linkedList, CancellationToken.None);
        var mainList = TaskList.Create(userId, "Main list", [TaskItem.Create("Depends on linked list", null, false, [linkedList.Id])]);
        await repository.AddAsync(mainList, CancellationToken.None);
        var handler = CreateHandler(repository);

        var taskLists = await handler.HandleAsync(new GetTaskListsQuery(userId), CancellationToken.None);

        var resolvedMainList = taskLists.Single(taskList => taskList.Id == mainList.Id);
        Assert.True(resolvedMainList.IsCompleted);
    }

    [Fact]
    public async Task HandleAsync_includes_task_lists_shared_via_an_accepted_grant_alongside_owned_lists()
    {
        var taskRepository = new InMemoryTaskRepository();
        var taskListShareRepository = new InMemoryTaskListShareRepository();
        var handler = CreateHandler(taskRepository, taskListShareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        await taskRepository.AddAsync(TaskList.Create(recipientId, "Mine", []), CancellationToken.None);
        var sharedList = TaskList.Create(ownerId, "Shared with me", []);
        await taskRepository.AddAsync(sharedList, CancellationToken.None);
        var share = TaskListShare.Create(sharedList.Id, ownerId, recipientId);
        share.MarkAccepted();
        await taskListShareRepository.AddAsync(share, CancellationToken.None);

        var taskLists = await handler.HandleAsync(new GetTaskListsQuery(recipientId), CancellationToken.None);

        Assert.Equal(2, taskLists.Count);
        Assert.Contains(taskLists, taskList => taskList.Title == "Shared with me" && taskList.IsShared);
    }

    /// <summary>
    /// A shared list arrives carrying the *recipient's* pin. The owner's says where it sits on the
    /// owner's page, and handing that over unchanged put a list somebody else had pinned at the top of
    /// this reader's - see TaskListAccessResolver, and the same pair of tests on the notes side.
    /// </summary>
    [Fact]
    public async Task HandleAsync_hands_a_recipient_their_own_pin_rather_than_the_owners()
    {
        var taskRepository = new InMemoryTaskRepository();
        var taskListShareRepository = new InMemoryTaskListShareRepository();
        var handler = CreateHandler(taskRepository, taskListShareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var sharedList = TaskList.Create(ownerId, "Shared with me", [], isPinned: true);
        await taskRepository.AddAsync(sharedList, CancellationToken.None);
        var share = TaskListShare.Create(sharedList.Id, ownerId, recipientId);
        share.MarkAccepted();
        await taskListShareRepository.AddAsync(share, CancellationToken.None);

        var taskLists = await handler.HandleAsync(new GetTaskListsQuery(recipientId), CancellationToken.None);

        Assert.False(Assert.Single(taskLists).IsPinnedForCaller);
    }

    /// <summary>
    /// And the recipient's own does reach them - stamped rather than stored, so it must not make the
    /// list look freshly changed either: that timestamp is what the phone syncs against.
    /// </summary>
    [Fact]
    public async Task HandleAsync_hands_a_recipient_their_own_pin_without_making_the_list_look_changed()
    {
        var taskRepository = new InMemoryTaskRepository();
        var taskListShareRepository = new InMemoryTaskListShareRepository();
        var handler = CreateHandler(taskRepository, taskListShareRepository);
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var sharedList = TaskList.Create(ownerId, "Shared with me", []);
        await taskRepository.AddAsync(sharedList, CancellationToken.None);
        var updatedBefore = sharedList.UpdatedAtUtc;
        var share = TaskListShare.Create(sharedList.Id, ownerId, recipientId);
        share.MarkAccepted();
        share.SetPinnedByRecipient(true);
        await taskListShareRepository.AddAsync(share, CancellationToken.None);

        var taskLists = await handler.HandleAsync(new GetTaskListsQuery(recipientId), CancellationToken.None);

        var resolved = Assert.Single(taskLists);
        Assert.True(resolved.IsPinnedForCaller);
        Assert.Equal(updatedBefore, resolved.UpdatedAtUtc);
    }

    /// <summary>
    /// And the stamp must not survive into a save - the notes side has the same test and the same
    /// reason. The resolver feeds the write paths too (UpdateTaskListCommandHandler resolves before it
    /// saves), so a recipient's pin written over the stored flag would be saved onto the owner's row the
    /// next time that recipient changed anything.
    /// </summary>
    [Fact]
    public async Task A_recipient_saving_a_shared_list_leaves_the_owners_pin_alone()
    {
        var taskRepository = new InMemoryTaskRepository();
        var taskListShareRepository = new InMemoryTaskListShareRepository();
        var resolver = new TaskListAccessResolver(
            taskRepository, taskListShareRepository, new InMemoryUserRepository());
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var sharedList = TaskList.Create(ownerId, "Shared with me", [], isPinned: true);
        await taskRepository.AddAsync(sharedList, CancellationToken.None);
        var share = TaskListShare.Create(sharedList.Id, ownerId, recipientId, ShareAccessLevel.CanEdit);
        share.MarkAccepted();
        await taskListShareRepository.AddAsync(share, CancellationToken.None);

        var resolved = (await resolver.ResolveAsync(recipientId, sharedList.Id, CancellationToken.None))!;
        resolved.Update("Shared with me", [], isGroup: false, isPrivate: false, null, ItemPriority.Normal);
        await taskRepository.UpdateAsync(resolved, CancellationToken.None);

        Assert.True((await taskRepository.GetByIdAsync(ownerId, sharedList.Id, CancellationToken.None))!.IsPinned);
    }
}
