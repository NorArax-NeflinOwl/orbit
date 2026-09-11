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
        => InventoryItem.Create(Guid.NewGuid(), name, "Part", ["Hardware"], quantity, minimumQuantity: null, InventoryUnit.Piece,
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

    /// <summary>An entry that describes a product, saying how little is too little and how much there is.</summary>
    private static TaskItem Asking(
        string description, decimal? minimum = null, decimal quantity = 0, bool isCompleted = false)
        => TaskItem.Create(
            description, dueDateUtc: null, isCompleted,
            subject: new TaskItemSubject(TaskItemKind.Inventory),
            product: TaskItemProduct.Default with { MinimumQuantity = minimum, Quantity = quantity });

    /// <summary>An entry that already stands for a row on a shelf.</summary>
    private static TaskItem StandingFor(InventoryItem shelfItem)
        => TaskItem.Create(
            shelfItem.Name, dueDateUtc: null, isCompleted: false,
            subject: new TaskItemSubject(TaskItemKind.Inventory, linkedInventoryItemId: shelfItem.Id));

    private static InventoryItem Stocked(string name, decimal quantity, decimal? minimumQuantity)
        => InventoryItem.Create(Guid.NewGuid(), name, "Part", ["Hardware"], quantity, minimumQuantity,
            InventoryUnit.Piece, expiryDate: null, NotificationChannel.None);

    /// <summary>Every occurrence still adds up - but by what it asks for, not by one.</summary>
    [Fact]
    public void Each_entry_adds_its_own_minimum()
    {
        var check = StockRequirementCounter.Count([Asking("Mąka", 2), Asking(" mąka ", 3)], [], Now);

        Assert.Equal(5, Assert.Single(check.Requirements).Required);
    }

    /// <summary>A line that says nothing is the counting rule's one, beside the one that did say.</summary>
    [Fact]
    public void An_entry_with_no_minimum_adds_one()
    {
        var check = StockRequirementCounter.Count([Asking("Mąka", 2), Asking("Mąka"), Work("Mąka")], [], Now);

        Assert.Equal(4, Assert.Single(check.Requirements).Required);
    }

    /// <summary>Several claims about one shelf: the smallest is the one that cannot be overstating it.</summary>
    [Fact]
    public void What_there_is_already_is_the_least_amount_written()
    {
        var requirement = Assert.Single(
            StockRequirementCounter.CountRegardlessOfDueDate([Asking("Mąka", quantity: 4), Asking("Mąka", quantity: 1)])
                .Requirements);

        Assert.Equal(1, requirement.SmallestAmountWritten);
        Assert.Equal(1, requirement.StartingStock);
    }

    /// <summary>Zero is the box nobody filled in, and it does not overrule an amount somebody did write.</summary>
    [Fact]
    public void An_amount_nobody_filled_in_takes_no_part()
    {
        var requirement = Assert.Single(
            StockRequirementCounter.CountRegardlessOfDueDate([Asking("Mąka", quantity: 4), Asking("Mąka")])
                .Requirements);

        Assert.Equal(4, requirement.StartingStock);
    }

    /// <summary>With no amount written anywhere, the crossed-off lines are still what says how much there is.</summary>
    [Fact]
    public void With_no_amount_written_the_crossed_off_lines_answer()
    {
        var requirement = Assert.Single(
            StockRequirementCounter.CountRegardlessOfDueDate(
                [Asking("Mąka", isCompleted: true), Asking("Mąka", isCompleted: true), Asking("Mąka")])
                .Requirements);

        Assert.Null(requirement.SmallestAmountWritten);
        Assert.Equal(2, requirement.StartingStock);
    }

    /// <summary>
    /// Entries that stand for one shelf item handed it their minimums when it was built, so its minimum
    /// is counted once between them - what makes the check read the number the shelf and its restock
    /// errand read.
    /// </summary>
    [Fact]
    public void Entries_standing_for_one_shelf_item_ask_for_its_minimum_once()
    {
        var flour = Stocked("Mąka", quantity: 2, minimumQuantity: 5);

        var check = StockRequirementCounter.Count([StandingFor(flour), StandingFor(flour)], [flour], Now);

        var requirement = Assert.Single(check.Requirements);
        Assert.Equal(5, requirement.Required);
        Assert.Equal(3, requirement.Missing);
    }

    /// <summary>A shelf item with no minimum was left to the counting rule, so its entries count one by one.</summary>
    [Fact]
    public void Entries_standing_for_a_shelf_item_with_no_minimum_are_counted_one_by_one()
    {
        var flour = Stocked("Mąka", quantity: 0, minimumQuantity: null);

        var check = StockRequirementCounter.Count([StandingFor(flour), StandingFor(flour)], [flour], Now);

        Assert.Equal(2, Assert.Single(check.Requirements).Required);
    }
}
