using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
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

    /// <summary>
    /// A group list is a way of reading several lists together. Getting rid of the reading is not the
    /// same as getting rid of what was being read, so the lists it gathers stay unless the caller has
    /// said otherwise - which is the question the task screens ask before sending this.
    /// </summary>
    [Fact]
    public async Task HandleAsync_leaves_the_lists_a_group_list_gathers_alone_by_default()
    {
        var repository = new InMemoryTaskRepository();
        var handler = NewHandler(repository);
        var userId = Guid.NewGuid();
        var gathered = TaskList.Create(userId, "Kitchen", []);
        await repository.AddAsync(gathered, CancellationToken.None);
        var group = GroupGathering(userId, gathered.Id);
        await repository.AddAsync(group, CancellationToken.None);

        await handler.HandleAsync(new DeleteTaskListCommand(userId, group.Id), CancellationToken.None);

        Assert.Null(await repository.GetByIdAsync(userId, group.Id, CancellationToken.None));
        Assert.NotNull(await repository.GetByIdAsync(userId, gathered.Id, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_deletes_the_whole_tree_when_the_caller_asked_for_it()
    {
        var repository = new InMemoryTaskRepository();
        var handler = NewHandler(repository);
        var userId = Guid.NewGuid();
        // Three deep, because a gathered list may gather in turn - deleting the top of a tree and
        // leaving its middle behind would answer "delete these too" with "some of them".
        var leaf = TaskList.Create(userId, "Bathroom", []);
        await repository.AddAsync(leaf, CancellationToken.None);
        var middle = GroupGathering(userId, leaf.Id);
        await repository.AddAsync(middle, CancellationToken.None);
        var group = GroupGathering(userId, middle.Id);
        await repository.AddAsync(group, CancellationToken.None);

        await handler.HandleAsync(
            new DeleteTaskListCommand(userId, group.Id, DeleteTheListsItGathers: true), CancellationToken.None);

        Assert.Null(await repository.GetByIdAsync(userId, group.Id, CancellationToken.None));
        Assert.Null(await repository.GetByIdAsync(userId, middle.Id, CancellationToken.None));
        Assert.Null(await repository.GetByIdAsync(userId, leaf.Id, CancellationToken.None));
    }

    /// <summary>
    /// A link pointing at somebody else's list, shared with this reader. Deleting that is not theirs to
    /// do, and "delete the lists it gathers" is not a way of asking for it.
    /// </summary>
    [Fact]
    public async Task HandleAsync_never_leaves_the_callers_own_lists_when_deleting_the_tree()
    {
        var repository = new InMemoryTaskRepository();
        var handler = NewHandler(repository);
        var userId = Guid.NewGuid();
        var somebodyElseId = Guid.NewGuid();
        var theirs = TaskList.Create(somebodyElseId, "Not yours", []);
        await repository.AddAsync(theirs, CancellationToken.None);
        var group = GroupGathering(userId, theirs.Id);
        await repository.AddAsync(group, CancellationToken.None);

        await handler.HandleAsync(
            new DeleteTaskListCommand(userId, group.Id, DeleteTheListsItGathers: true), CancellationToken.None);

        Assert.Null(await repository.GetByIdAsync(userId, group.Id, CancellationToken.None));
        Assert.NotNull(await repository.GetByIdAsync(somebodyElseId, theirs.Id, CancellationToken.None));
    }

    /// <summary>
    /// Two lists gathering each other. Refused when they are made (see TaskListLinkValidator), so this
    /// is about a pair that slipped through some other way - a tree read without a visited set would
    /// hang on it rather than deleting anything.
    /// </summary>
    [Fact]
    public async Task HandleAsync_does_not_loop_on_two_lists_that_gather_each_other()
    {
        var repository = new InMemoryTaskRepository();
        var handler = NewHandler(repository);
        var userId = Guid.NewGuid();
        var first = TaskList.Create(userId, "First", []);
        await repository.AddAsync(first, CancellationToken.None);
        var second = GroupGathering(userId, first.Id);
        await repository.AddAsync(second, CancellationToken.None);
        first.Update(
            "First", [TaskItem.Create("Follows the second", null, false, [second.Id])], isGroup: true,
            isPrivate: false, encryptedContent: null, priority: ItemPriority.Normal);
        await repository.UpdateAsync(first, CancellationToken.None);

        await handler.HandleAsync(
            new DeleteTaskListCommand(userId, first.Id, DeleteTheListsItGathers: true), CancellationToken.None);

        Assert.Null(await repository.GetByIdAsync(userId, first.Id, CancellationToken.None));
        Assert.Null(await repository.GetByIdAsync(userId, second.Id, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_returns_false_for_an_unknown_task_list_id()
    {
        var handler = new DeleteTaskListCommandHandler(new InMemoryTaskRepository(), new InMemoryTaskListShareRepository(), new InMemorySyncTombstoneRepository());

        var wasDeleted = await handler.HandleAsync(new DeleteTaskListCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(wasDeleted);
    }

    private static DeleteTaskListCommandHandler NewHandler(InMemoryTaskRepository repository)
        => new(repository, new InMemoryTaskListShareRepository(), new InMemorySyncTombstoneRepository());

    private static TaskList GroupGathering(Guid userId, params Guid[] gatheredIds)
        => TaskList.Create(
            userId, "Everything", [TaskItem.Create("Follows other lists", null, false, gatheredIds)], isGroup: true);
}
