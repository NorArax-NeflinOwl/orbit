using Orbit.Core.Inventories;
using Xunit;

namespace Orbit.Api.Tests.Inventories;

/// <summary>
/// Whether building a storage out of a task list is worth offering. Both clients ask this before
/// drawing the menu entry: the only list it is refused on is one made of links to lists with nothing
/// on them, where there is genuinely nothing for a shelf to hold.
/// </summary>
public sealed class GeneratedInventorySourceTests
{
    /// <summary>
    /// A shopping list of plain lines is exactly what somebody wants a shelf built from, and the
    /// endpoint builds one out of every entry the tree names. The rule asked for a *product* until
    /// 2026-09-18, which hid the offer from the lists it is most useful on.
    /// </summary>
    [Fact]
    public void A_list_of_plain_errands_is_a_shelf_waiting_to_be_built()
        => Assert.True(GeneratedInventorySource.HasSomethingToBuildFrom(
            [Errand(), Errand()], _ => null));

    [Fact]
    public void One_product_on_the_list_is_enough()
        => Assert.True(GeneratedInventorySource.HasSomethingToBuildFrom(
            [Errand(), Product()], _ => null));

    /// <summary>
    /// A group list holds no work of its own - what it stands for is on the lists it gathers, and that
    /// is where a shelf built from it would come from too.
    /// </summary>
    [Fact]
    public void A_list_standing_for_one_with_a_product_on_it_counts()
    {
        var shopping = Guid.NewGuid();

        Assert.True(GeneratedInventorySource.HasSomethingToBuildFrom(
            [StandsFor(shopping)],
            taskListId => taskListId == shopping ? [Product()] : null));
    }

    /// <summary>And one gathering lists of plain errands counts too, for the reason above.</summary>
    [Fact]
    public void A_list_standing_for_one_with_only_errands_on_it_counts_as_well()
    {
        var chores = Guid.NewGuid();

        Assert.True(GeneratedInventorySource.HasSomethingToBuildFrom(
            [StandsFor(chores)],
            taskListId => taskListId == chores ? [Errand()] : null));
    }

    /// <summary>Nothing anywhere below it: links all the way down to lists holding no work at all.</summary>
    [Fact]
    public void A_list_of_links_to_empty_lists_has_nothing_to_build_from()
    {
        var middle = Guid.NewGuid();
        var empty = Guid.NewGuid();

        Assert.False(GeneratedInventorySource.HasSomethingToBuildFrom(
            [StandsFor(middle)],
            taskListId => taskListId == middle ? [StandsFor(empty)] : taskListId == empty ? [] : null));
    }

    /// <summary>However deep it goes: a group list of group lists is still a way of reading the work.</summary>
    [Fact]
    public void It_walks_the_whole_tree()
    {
        var middle = Guid.NewGuid();
        var shopping = Guid.NewGuid();

        Assert.True(GeneratedInventorySource.HasSomethingToBuildFrom(
            [StandsFor(middle)],
            taskListId => taskListId == middle ? [StandsFor(shopping)] : taskListId == shopping ? [Product()] : null));
    }

    /// <summary>
    /// A list this client has not got - never synced, or a share withdrawn - is read as nothing to
    /// build from, which is all that can honestly be said about a list nobody here can see.
    /// </summary>
    [Fact]
    public void A_list_nobody_here_holds_says_nothing_either_way()
        => Assert.False(GeneratedInventorySource.HasSomethingToBuildFrom(
            [StandsFor(Guid.NewGuid())], _ => null));

    /// <summary>
    /// Two lists standing for each other are refused when they are made, and would hang this if one
    /// ever slipped through - the same reason deleting a tree carries a visited set.
    /// </summary>
    [Fact]
    public void A_pair_of_lists_standing_for_each_other_does_not_hang_it()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        Assert.False(GeneratedInventorySource.HasSomethingToBuildFrom(
            [StandsFor(second)],
            taskListId => taskListId == second ? [StandsFor(first)] : taskListId == first ? [StandsFor(second)] : null));
    }

    private static TaskEntrySummary Errand() => new(IsAProduct: false, []);

    private static TaskEntrySummary Product() => new(IsAProduct: true, []);

    private static TaskEntrySummary StandsFor(Guid taskListId) => new(IsAProduct: false, [taskListId]);
}
