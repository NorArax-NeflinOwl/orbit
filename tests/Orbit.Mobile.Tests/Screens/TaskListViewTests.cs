using Orbit.Mobile.Data;
using Orbit.Mobile.Screens.Tasks;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Which task lists are shown and in what order - neither of which the phone could decide. The web's
/// task page makes the same choices, so the two do not disagree about what "Overdue" means or where a
/// pinned list sits.
/// </summary>
public sealed class TaskListViewTests
{
    [Fact]
    public void With_no_filter_everything_is_shown()
    {
        var lists = new[] { List("A", status: "New"), List("B", status: "Completed") };

        var shown = TaskListView.Arrange(lists, status: null, TaskListArrangement.By(TaskListSortOrder.Alphabetical));

        Assert.Equal(["A", "B"], shown.Select(list => list.Title));
    }

    [Fact]
    public void A_status_filter_leaves_only_that_status()
    {
        var lists = new[] { List("A", status: "New"), List("B", status: "Completed") };

        var shown = TaskListView.Arrange(lists, "Completed", TaskListArrangement.By(TaskListSortOrder.Alphabetical));

        Assert.Equal(["B"], shown.Select(list => list.Title));
    }

    [Fact]
    public void Alphabetical_and_its_reverse_are_opposites()
    {
        var lists = new[] { List("Beta"), List("alpha") };

        Assert.Equal(
            ["alpha", "Beta"],
            TaskListView.Arrange(lists, null, TaskListArrangement.By(TaskListSortOrder.Alphabetical))
                .Select(list => list.Title));
        Assert.Equal(
            ["Beta", "alpha"],
            TaskListView.Arrange(lists, null, TaskListArrangement.By(TaskListSortOrder.ReverseAlphabetical))
                .Select(list => list.Title));
    }

    [Fact]
    public void Priority_puts_the_highest_first()
    {
        var lists = new[] { List("Low", priority: "Low"), List("Highest", priority: "Highest"), List("Normal") };

        var shown = TaskListView.Arrange(lists, null, TaskListArrangement.By(TaskListSortOrder.Priority));

        Assert.Equal(["Highest", "Normal", "Low"], shown.Select(list => list.Title));
    }

    /// <summary>
    /// The same list, upside down - for a reader clearing the small things out of the way rather than
    /// starting on the big one. Orbit.Web offers both halves and the phone offered only the one.
    /// </summary>
    [Fact]
    public void The_least_important_can_be_put_first_instead()
    {
        var lists = new[] { List("Low", priority: "Low"), List("Highest", priority: "Highest"), List("Normal") };

        var shown = TaskListView.Arrange(lists, null, TaskListArrangement.By(TaskListSortOrder.LeastImportantFirst));

        Assert.Equal(["Low", "Normal", "Highest"], shown.Select(list => list.Title));
    }

    /// <summary>An unknown priority sorts as Normal - one added in a later build must not be a crash.</summary>
    [Fact]
    public void An_unknown_priority_sorts_as_normal()
    {
        var lists = new[] { List("Odd", priority: "Whatever"), List("High", priority: "High") };

        var shown = TaskListView.Arrange(lists, null, TaskListArrangement.By(TaskListSortOrder.Priority));

        Assert.Equal(["High", "Odd"], shown.Select(list => list.Title));
    }

    /// <summary>
    /// Pinning is the reader saying "this one, above the rule". A sort that ignored it would take the
    /// pin away in all but name.
    /// </summary>
    [Fact]
    public void A_pinned_list_comes_first_whatever_the_order()
    {
        var lists = new[] { List("alpha"), List("zeta", isPinned: true) };

        Assert.Equal(
            ["zeta", "alpha"],
            TaskListView.Arrange(lists, null, TaskListArrangement.By(TaskListSortOrder.Alphabetical))
                .Select(list => list.Title));
    }

    [Fact]
    public void Newest_and_oldest_are_opposites()
    {
        var older = List("older", created: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var newer = List("newer", created: DateTimeOffset.Parse("2026-08-01T00:00:00Z"));
        var lists = new[] { older, newer };

        Assert.Equal(
            ["newer", "older"],
            TaskListView.Arrange(lists, null, TaskListArrangement.By(TaskListSortOrder.Newest))
                .Select(list => list.Title));
        Assert.Equal(
            ["older", "newer"],
            TaskListView.Arrange(lists, null, TaskListArrangement.By(TaskListSortOrder.Oldest))
                .Select(list => list.Title));
    }

    /// <summary>
    /// The order the reader put the cards in is the one exception to pinning: it already says where
    /// every card goes, so a pin would contradict it. Orbit.Web draws the line in the same place.
    /// </summary>
    [Fact]
    public void The_readers_own_order_is_not_overruled_by_a_pin()
    {
        var alpha = List("alpha");
        var zeta = List("zeta", isPinned: true);

        var shown = TaskListView.Arrange(
            [alpha, zeta], null, new TaskListArrangement(TaskListSortOrder.Manual, [alpha.LocalId, zeta.LocalId]));

        Assert.Equal(["alpha", "zeta"], shown.Select(list => list.Title));
    }

    /// <summary>
    /// A list made or shared since the reader last moved one is not in the wrong place - it is simply
    /// not placed yet, and it goes after the ones that are rather than pushing their order about.
    /// </summary>
    [Fact]
    public void A_list_nobody_has_placed_comes_after_the_ones_they_have()
    {
        var placed = List("placed");
        var brandNew = List("brand new");

        var shown = TaskListView.Arrange(
            [brandNew, placed], null, new TaskListArrangement(TaskListSortOrder.Manual, [placed.LocalId]));

        Assert.Equal(["placed", "brand new"], shown.Select(list => list.Title));
    }

    private static LocalTaskList List(
        string title, string status = "New", string priority = "Normal", bool isPinned = false,
        DateTimeOffset? created = null)
        => new()
        {
            LocalId = Guid.NewGuid(),
            Title = title,
            Status = status,
            Priority = priority,
            IsPinned = isPinned,
            CreatedAtUtc = created ?? DateTimeOffset.Parse("2026-06-01T00:00:00Z")
        };
}
