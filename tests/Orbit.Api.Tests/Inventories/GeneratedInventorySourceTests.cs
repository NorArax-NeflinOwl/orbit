using Orbit.Core.Inventories;
using Xunit;

namespace Orbit.Api.Tests.Inventories;

/// <summary>
/// Whether building a storage out of a task list is worth offering. Both clients ask this before
/// drawing the menu entry: on a list of plain errands it was an offer to build an empty storage and
/// quietly point the list at it.
/// </summary>
public sealed class GeneratedInventorySourceTests
{
    [Fact]
    public void A_list_of_plain_errands_has_nothing_a_shelf_would_be_about()
        => Assert.False(GeneratedInventorySource.HasSomethingToBuildFrom(
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

    [Fact]
    public void A_list_standing_for_one_with_only_errands_on_it_does_not()
    {
        var chores = Guid.NewGuid();

        Assert.False(GeneratedInventorySource.HasSomethingToBuildFrom(
            [StandsFor(chores)],
            taskListId => taskListId == chores ? [Errand()] : null));
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
