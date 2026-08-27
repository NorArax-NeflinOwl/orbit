using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Moving a row to where another one sits, which is what dragging an item about in an editor does.
/// </summary>
public sealed class DragReorderingTests
{
    /// <summary>A row is whatever the editor is rendering; only its identity matters here.</summary>
    private sealed class Row(string name)
    {
        public string Name { get; } = name;
    }

    private static List<Row> Rows(params string[] names) => [.. names.Select(name => new Row(name))];

    private static IReadOnlyList<string> NamesIn(IEnumerable<Row> rows) => [.. rows.Select(row => row.Name)];

    [Fact]
    public void A_row_dropped_further_down_lands_where_the_target_was()
    {
        var rows = Rows("Milk", "Bread", "Cheese");
        var reordering = new DragReordering<Row>();
        reordering.Start(rows[0]);

        Assert.True(reordering.DropOn(rows, rows[2]));

        Assert.Equal(["Bread", "Cheese", "Milk"], NamesIn(rows));
    }

    [Fact]
    public void A_row_dropped_further_up_pushes_the_rest_down()
    {
        var rows = Rows("Milk", "Bread", "Cheese");
        var reordering = new DragReordering<Row>();
        reordering.Start(rows[2]);

        Assert.True(reordering.DropOn(rows, rows[0]));

        Assert.Equal(["Cheese", "Milk", "Bread"], NamesIn(rows));
    }

    [Fact]
    public void Dropping_a_row_on_itself_changes_nothing()
    {
        var rows = Rows("Milk", "Bread");
        var reordering = new DragReordering<Row>();
        reordering.Start(rows[0]);

        Assert.False(reordering.DropOn(rows, rows[0]));

        Assert.Equal(["Milk", "Bread"], NamesIn(rows));
    }

    [Fact]
    public void A_drop_with_nothing_being_dragged_changes_nothing()
    {
        var rows = Rows("Milk", "Bread");

        Assert.False(new DragReordering<Row>().DropOn(rows, rows[1]));

        Assert.Equal(["Milk", "Bread"], NamesIn(rows));
    }

    [Fact]
    public void A_row_that_was_removed_while_being_dragged_is_not_put_back()
    {
        var rows = Rows("Milk", "Bread");
        var dragged = rows[0];
        var reordering = new DragReordering<Row>();
        reordering.Start(dragged);
        rows.Remove(dragged);

        Assert.False(reordering.DropOn(rows, rows[0]));

        Assert.Equal(["Bread"], NamesIn(rows));
    }

    [Fact]
    public void Two_rows_saying_the_same_thing_are_still_told_apart()
    {
        // The whole reason rows are matched by reference: a shopping list naming milk twice must move
        // the one that was picked up, not the first one that reads the same.
        var rows = Rows("Milk", "Bread", "Milk");
        var secondMilk = rows[2];
        var firstMilk = rows[0];
        var reordering = new DragReordering<Row>();
        reordering.Start(secondMilk);

        Assert.True(reordering.DropOn(rows, firstMilk));

        Assert.Same(secondMilk, rows[0]);
        Assert.Same(firstMilk, rows[1]);
    }

    [Fact]
    public void Finishing_a_drag_leaves_nothing_being_dragged()
    {
        var rows = Rows("Milk", "Bread");
        var reordering = new DragReordering<Row>();
        reordering.Start(rows[0]);
        Assert.True(reordering.IsDragging(rows[0]));

        reordering.Finish();

        Assert.False(reordering.IsDragging(rows[0]));
        Assert.False(reordering.DropOn(rows, rows[1]));
    }
}
