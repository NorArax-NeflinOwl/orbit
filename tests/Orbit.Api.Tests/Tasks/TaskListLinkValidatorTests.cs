using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

public sealed class TaskListLinkValidatorTests
{
    [Fact]
    public async Task ValidateAsync_does_nothing_when_no_item_links_to_another_list()
    {
        var validator = new TaskListLinkValidator(new InMemoryTaskRepository());
        var items = new[] { TaskItem.Create("Buy milk", null, false) };

        // Should not throw.
        await validator.ValidateAsync(Guid.NewGuid(), taskListId: null, items, CancellationToken.None);
    }

    [Fact]
    public async Task ValidateAsync_accepts_a_link_to_another_list_owned_by_the_same_user()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        var otherList = TaskList.Create(userId, "Other list", []);
        await repository.AddAsync(otherList, CancellationToken.None);
        var validator = new TaskListLinkValidator(repository);
        var items = new[] { TaskItem.Create("Depends on other list", null, false, otherList.Id) };

        // Should not throw.
        await validator.ValidateAsync(userId, taskListId: null, items, CancellationToken.None);
    }

    [Fact]
    public async Task ValidateAsync_rejects_an_item_linking_to_the_list_it_belongs_to()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        var taskList = TaskList.Create(userId, "Errands", []);
        await repository.AddAsync(taskList, CancellationToken.None);
        var validator = new TaskListLinkValidator(repository);
        var items = new[] { TaskItem.Create("Self reference", null, false, taskList.Id) };

        await Assert.ThrowsAsync<ArgumentException>(
            () => validator.ValidateAsync(userId, taskList.Id, items, CancellationToken.None));
    }

    [Fact]
    public async Task ValidateAsync_rejects_a_link_to_an_unknown_task_list_id()
    {
        var validator = new TaskListLinkValidator(new InMemoryTaskRepository());
        var items = new[] { TaskItem.Create("Depends on nothing", null, false, Guid.NewGuid()) };

        await Assert.ThrowsAsync<ArgumentException>(
            () => validator.ValidateAsync(Guid.NewGuid(), taskListId: null, items, CancellationToken.None));
    }

    [Fact]
    public async Task ValidateAsync_rejects_a_link_to_a_list_owned_by_a_different_user()
    {
        var repository = new InMemoryTaskRepository();
        var otherUsersList = TaskList.Create(Guid.NewGuid(), "Not mine", []);
        await repository.AddAsync(otherUsersList, CancellationToken.None);
        var validator = new TaskListLinkValidator(repository);
        var items = new[] { TaskItem.Create("Depends on someone else's list", null, false, otherUsersList.Id) };

        await Assert.ThrowsAsync<ArgumentException>(
            () => validator.ValidateAsync(Guid.NewGuid(), taskListId: null, items, CancellationToken.None));
    }

    [Fact]
    public async Task ValidateAsync_rejects_a_link_that_would_close_a_cycle_between_two_lists()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        var listB = TaskList.Create(userId, "List B", []);
        await repository.AddAsync(listB, CancellationToken.None);
        // List A already links to List B.
        var listA = TaskList.Create(userId, "List A", [TaskItem.Create("Depends on B", null, false, listB.Id)]);
        await repository.AddAsync(listA, CancellationToken.None);
        var validator = new TaskListLinkValidator(repository);

        // Now trying to make List B link back to List A would close the loop.
        var itemsForB = new[] { TaskItem.Create("Depends on A", null, false, listA.Id) };

        await Assert.ThrowsAsync<ArgumentException>(
            () => validator.ValidateAsync(userId, listB.Id, itemsForB, CancellationToken.None));
    }

    [Fact]
    public async Task ValidateAsync_accepts_a_non_cyclic_chain_of_links_across_three_lists()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        var listC = TaskList.Create(userId, "List C", []);
        await repository.AddAsync(listC, CancellationToken.None);
        var listB = TaskList.Create(userId, "List B", [TaskItem.Create("Depends on C", null, false, listC.Id)]);
        await repository.AddAsync(listB, CancellationToken.None);
        var validator = new TaskListLinkValidator(repository);

        var itemsForA = new[] { TaskItem.Create("Depends on B", null, false, listB.Id) };

        // Should not throw: A -> B -> C is a chain, not a cycle.
        await validator.ValidateAsync(userId, taskListId: null, itemsForA, CancellationToken.None);
    }
}
