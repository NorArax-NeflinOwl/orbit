using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

public sealed class LinkedTaskCompletionResolverTests
{
    private readonly LinkedTaskCompletionResolver _resolver = new();

    [Fact]
    public void ResolveAll_leaves_a_list_with_no_linked_items_unchanged()
    {
        var userId = Guid.NewGuid();
        var taskList = TaskList.Create(userId, "Errands", [TaskItem.Create("Buy milk", null, true)]);

        var resolved = _resolver.ResolveAll([taskList]);

        var resolvedList = Assert.Single(resolved);
        Assert.True(resolvedList.IsCompleted);
    }

    [Fact]
    public void ResolveAll_resolves_a_linked_item_to_not_completed_when_the_linked_list_is_not_fully_done()
    {
        var userId = Guid.NewGuid();
        var linkedList = TaskList.Create(userId, "Linked list", [TaskItem.Create("Not done", null, false)]);
        var mainList = TaskList.Create(userId, "Main list", [TaskItem.Create("Depends on linked list", null, false, [linkedList.Id])]);

        var resolved = _resolver.ResolveAll([mainList, linkedList]);

        var resolvedMainList = resolved.Single(taskList => taskList.Id == mainList.Id);
        Assert.False(Assert.Single(resolvedMainList.Items).IsCompleted);
        Assert.False(resolvedMainList.IsCompleted);
    }

    [Fact]
    public void ResolveAll_resolves_a_linked_item_to_completed_when_the_linked_list_is_fully_done()
    {
        var userId = Guid.NewGuid();
        var linkedList = TaskList.Create(userId, "Linked list", [TaskItem.Create("Done", null, true)]);
        var mainList = TaskList.Create(userId, "Main list", [TaskItem.Create("Depends on linked list", null, false, [linkedList.Id])]);

        var resolved = _resolver.ResolveAll([mainList, linkedList]);

        var resolvedMainList = resolved.Single(taskList => taskList.Id == mainList.Id);
        Assert.True(Assert.Single(resolvedMainList.Items).IsCompleted);
        Assert.True(resolvedMainList.IsCompleted);
    }

    [Fact]
    public void ResolveAll_resolves_transitively_through_a_chain_of_linked_lists()
    {
        var userId = Guid.NewGuid();
        var listC = TaskList.Create(userId, "List C", [TaskItem.Create("Plain item", null, true)]);
        var listB = TaskList.Create(userId, "List B", [TaskItem.Create("Depends on C", null, false, [listC.Id])]);
        var listA = TaskList.Create(userId, "List A", [TaskItem.Create("Depends on B", null, false, [listB.Id])]);

        var resolved = _resolver.ResolveAll([listA, listB, listC]);

        var resolvedA = resolved.Single(taskList => taskList.Id == listA.Id);
        var resolvedB = resolved.Single(taskList => taskList.Id == listB.Id);
        Assert.True(resolvedB.IsCompleted);
        Assert.True(resolvedA.IsCompleted);
    }

    [Fact]
    public void ResolveAll_does_not_recurse_forever_when_two_lists_link_to_each_other()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var listXId = Guid.NewGuid();
        var listYId = Guid.NewGuid();
        // TaskListLinkValidator is what normally prevents this from ever being saved - this constructs
        // the scenario directly to confirm the resolver itself has a defensive backstop.
        var listX = TaskList.FromPersistence(
            listXId, userId, "X", [TaskItem.Create("Depends on Y", null, false, [listYId])], isGroup: false, isPrivate: false, encryptedContent: null, now, now, null, null, null, ItemPriority.Normal, isPinned: false);
        var listY = TaskList.FromPersistence(
            listYId, userId, "Y", [TaskItem.Create("Depends on X", null, false, [listXId])], isGroup: false, isPrivate: false, encryptedContent: null, now, now, null, null, null, ItemPriority.Normal, isPinned: false);

        var resolved = _resolver.ResolveAll([listX, listY]);

        Assert.Equal(2, resolved.Count);
        Assert.All(resolved, taskList => Assert.False(taskList.IsCompleted));
    }
}
