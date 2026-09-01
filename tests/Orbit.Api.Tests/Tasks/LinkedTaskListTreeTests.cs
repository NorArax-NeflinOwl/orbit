using Orbit.Core.Tasks;
using Orbit.Core.Tasks.StockCheck;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// The server's own walk down a group list's linked lists - the same shape the checklist screen draws,
/// done here because the stock check and the inventory generator both count the whole tree.
/// </summary>
public sealed class LinkedTaskListTreeTests
{
    private static TaskList List(string title, bool isGroup, params TaskItem[] items)
        => TaskList.Create(Guid.NewGuid(), title, items, isGroup);

    private static TaskItem Work(string description) => TaskItem.Create(description, dueDateUtc: null, isCompleted: false);

    private static TaskItem LinkTo(TaskList target)
        => TaskItem.Create($"{target.Title} done", dueDateUtc: null, isCompleted: false, linkedTaskListIds: [target.Id]);

    [Fact]
    public void The_work_of_the_whole_tree_is_gathered()
    {
        var tiling = List("Tiling", isGroup: false, Work("Grout"));
        var kitchen = List("Kitchen", isGroup: true, LinkTo(tiling), Work("Hinge"));
        var renovation = List("Renovation", isGroup: true, LinkTo(kitchen), Work("Screw"));

        var work = LinkedTaskListTree.WorkIn(renovation, [renovation, kitchen, tiling]);

        Assert.Equal(["Screw", "Hinge", "Grout"], work.Select(item => item.Description));
    }

    [Fact]
    public void A_row_that_only_points_at_another_list_is_not_work()
    {
        var kitchen = List("Kitchen", isGroup: false, Work("Hinge"));
        var renovation = List("Renovation", isGroup: true, LinkTo(kitchen));

        var work = LinkedTaskListTree.WorkIn(renovation, [renovation, kitchen]);

        Assert.Equal(["Hinge"], work.Select(item => item.Description));
    }

    [Fact]
    public void A_list_that_links_back_to_an_ancestor_does_not_unfold_forever()
    {
        var second = List("Second", isGroup: true, Work("Bolt"));
        var first = List("First", isGroup: true, LinkTo(second), Work("Nut"));
        second.Update(second.Title, [.. second.Items, TaskItem.Create("Back", null, false, [first.Id])],
            second.IsGroup, second.IsPrivate, second.EncryptedContent, second.Priority);

        var gathered = LinkedTaskListTree.Flatten(first, [first, second]);

        Assert.Equal(["First", "Second"], gathered.Select(list => list.Title));
    }

    [Fact]
    public void A_list_that_is_not_a_group_is_the_whole_tree()
    {
        var other = List("Other", isGroup: false, Work("Something"));
        var plain = List("Plain", isGroup: false, Work("Screw"), TaskItem.Create("Ignored link", null, false, [other.Id]));

        var gathered = LinkedTaskListTree.Flatten(plain, [plain, other]);

        // A link on a list that is not a group is not followed - only a group list gathers members.
        Assert.Equal(["Plain"], gathered.Select(list => list.Title));
    }

    [Fact]
    public void A_link_pointing_at_nothing_reachable_is_skipped()
    {
        var group = List("Group", isGroup: true, TaskItem.Create("Gone", null, false, [Guid.NewGuid()]), Work("Screw"));

        var work = LinkedTaskListTree.WorkIn(group, [group]);

        Assert.Equal(["Screw"], work.Select(item => item.Description));
    }
}
