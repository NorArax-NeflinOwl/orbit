using Orbit.Core.Inventories;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.StockCheck;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// The counting rule behind "can this list actually be done": repetition is quantity, and what is not
/// due yet is not counted.
/// </summary>
public sealed class StockRequirementCounterTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static TaskItem Work(string description, DateTimeOffset? dueDateUtc = null)
        => TaskItem.Create(description, dueDateUtc, isCompleted: false);

    private static InventoryItem Stock(string name, decimal quantity)
        => InventoryItem.Create(Guid.NewGuid(), name, "Part", "Hardware", quantity, minimumQuantity: null, InventoryUnit.Piece,
            expiryDate: null, NotificationChannel.None);

    [Fact]
    public void Saying_the_same_thing_three_times_asks_for_three()
    {
        var check = StockRequirementCounter.Count(
            [Work("Screw"), Work("Screw"), Work("Screw")], [Stock("Screw", 10)], Now);

        var requirement = Assert.Single(check.Requirements);
        Assert.Equal(3, requirement.Required);
        Assert.Equal(10, requirement.Available);
        Assert.True(check.IsAchievable);
    }

    [Fact]
    public void A_shelf_that_falls_short_says_by_how_much()
    {
        var check = StockRequirementCounter.Count(
            [Work("Hinge"), Work("Hinge"), Work("Hinge")], [Stock("Hinge", 1)], Now);

        Assert.False(check.IsAchievable);
        var shortfall = Assert.Single(check.Shortfalls);
        Assert.Equal("Hinge", shortfall.Name);
        Assert.Equal(2, shortfall.Missing);
    }

    [Fact]
    public void Something_the_inventory_has_never_heard_of_is_missing_in_full()
    {
        var check = StockRequirementCounter.Count([Work("Brass handle")], [Stock("Screw", 100)], Now);

        Assert.Equal(1, Assert.Single(check.Shortfalls).Missing);
    }

    [Fact]
    public void Work_that_is_not_due_yet_is_not_counted()
    {
        // A line dated next week is work that has not come round; counting it would raise a restock task
        // for something nobody is about to start.
        var check = StockRequirementCounter.Count(
            [Work("Screw"), Work("Screw", Now.AddDays(7))], [Stock("Screw", 1)], Now);

        Assert.Equal(1, Assert.Single(check.Requirements).Required);
        Assert.True(check.IsAchievable);
    }

    [Fact]
    public void Work_that_is_already_due_still_counts()
    {
        var check = StockRequirementCounter.Count(
            [Work("Screw", Now.AddDays(-1)), Work("Screw", Now)], [Stock("Screw", 1)], Now);

        Assert.Equal(2, Assert.Single(check.Requirements).Required);
        Assert.False(check.IsAchievable);
    }

    [Fact]
    public void A_row_that_only_points_at_another_list_is_not_work()
    {
        // That row is how a group list is held together - it is not a thing to fetch off a shelf.
        var link = TaskItem.Create("Kitchen done", dueDateUtc: null, isCompleted: false, linkedTaskListIds: [Guid.NewGuid()]);

        var check = StockRequirementCounter.Count([link, Work("Screw")], [Stock("Screw", 5)], Now);

        Assert.Equal("Screw", Assert.Single(check.Requirements).Name);
    }

    [Fact]
    public void The_same_thing_written_differently_is_still_the_same_thing()
    {
        var check = StockRequirementCounter.Count(
            [Work("screw"), Work(" Screw "), Work("SCREW")], [Stock("Screw", 2)], Now);

        var requirement = Assert.Single(check.Requirements);
        Assert.Equal(3, requirement.Required);
        // The first spelling is the one shown back, rather than the flattened key.
        Assert.Equal("screw", requirement.Name);
    }

    [Fact]
    public void Two_shelves_of_the_same_thing_add_up()
    {
        var check = StockRequirementCounter.Count(
            [Work("Screw"), Work("Screw")], [Stock("Screw", 1), Stock("screw", 1)], Now);

        Assert.True(check.IsAchievable);
    }

    [Fact]
    public void The_worst_shortfall_is_reported_first()
    {
        var check = StockRequirementCounter.Count(
            [Work("Hinge"), Work("Screw"), Work("Screw"), Work("Screw")], [Stock("Hinge", 0), Stock("Screw", 0)], Now);

        Assert.Equal(["Screw", "Hinge"], check.Shortfalls.Select(shortfall => shortfall.Name));
    }

    [Fact]
    public void A_list_with_nothing_on_it_asks_for_nothing()
    {
        var check = StockRequirementCounter.Count([], [Stock("Screw", 5)], Now);

        Assert.Empty(check.Requirements);
        Assert.True(check.IsAchievable);
    }
}
