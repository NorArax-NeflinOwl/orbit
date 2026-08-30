using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Moving a row to where another one sits, which is what dragging an item about in an editor does.
/// </summary>
public sealed class RowArrangementTests
{
    /// <summary>A row is whatever the editor is rendering; only its identity matters here.</summary>

    [Fact]
    public void A_row_can_be_moved_one_place_up()
    {
        var rows = new List<Row> { new("first"), new("second"), new("third") };

        Assert.True(RowArrangement<Row>.Move(rows, rows[2], -1));

        Assert.Equal(["first", "third", "second"], rows.Select(row => row.Name));
    }

    [Fact]
    public void A_row_can_be_moved_one_place_down()
    {
        var rows = new List<Row> { new("first"), new("second"), new("third") };

        Assert.True(RowArrangement<Row>.Move(rows, rows[0], 1));

        Assert.Equal(["second", "first", "third"], rows.Select(row => row.Name));
    }

    [Fact]
    public void The_top_row_does_not_wrap_round_to_the_bottom()
    {
        // Which is not what "up" means to anybody looking at the first row.
        var rows = new List<Row> { new("first"), new("second") };

        Assert.False(RowArrangement<Row>.Move(rows, rows[0], -1));
        Assert.Equal(["first", "second"], rows.Select(row => row.Name));
    }

    [Fact]
    public void The_bottom_row_does_not_wrap_round_to_the_top()
    {
        var rows = new List<Row> { new("first"), new("second") };

        Assert.False(RowArrangement<Row>.Move(rows, rows[1], 1));
        Assert.Equal(["first", "second"], rows.Select(row => row.Name));
    }

    [Fact]
    public void A_row_the_list_does_not_hold_moves_nothing()
    {
        var rows = new List<Row> { new("first"), new("second") };

        Assert.False(RowArrangement<Row>.Move(rows, new Row("elsewhere"), -1));
        Assert.Equal(["first", "second"], rows.Select(row => row.Name));
    }

    [Fact]
    public void Whether_a_move_would_land_anywhere_is_answerable_without_making_it()
    {
        // What greys the button out, so the reader is not offered a move that does nothing.
        var rows = new List<Row> { new("first"), new("second"), new("third") };

        Assert.False(RowArrangement<Row>.CanMove(rows, rows[0], -1));
        Assert.True(RowArrangement<Row>.CanMove(rows, rows[0], 1));
        Assert.True(RowArrangement<Row>.CanMove(rows, rows[2], -1));
        Assert.False(RowArrangement<Row>.CanMove(rows, rows[2], 1));
    }

    [Fact]
    public void The_row_that_was_picked_is_the_one_that_moves_even_among_twins()
    {
        // Matched by reference, the same as a drag - a list naming the same thing twice still moves the
        // row that was asked about rather than the first match.
        var second = new Row("same");
        var rows = new List<Row> { new("same"), second, new("last") };

        Assert.True(RowArrangement<Row>.Move(rows, second, 1));

        Assert.Same(second, rows[2]);
    }
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
        var arrangement = new RowArrangement<Row>();
        arrangement.Start(rows[0]);

        Assert.True(arrangement.DropOn(rows, rows[2]));

        Assert.Equal(["Bread", "Cheese", "Milk"], NamesIn(rows));
    }

    [Fact]
    public void A_row_dropped_further_up_pushes_the_rest_down()
    {
        var rows = Rows("Milk", "Bread", "Cheese");
        var arrangement = new RowArrangement<Row>();
        arrangement.Start(rows[2]);

        Assert.True(arrangement.DropOn(rows, rows[0]));

        Assert.Equal(["Cheese", "Milk", "Bread"], NamesIn(rows));
    }

    [Fact]
    public void Dropping_a_row_on_itself_changes_nothing()
    {
        var rows = Rows("Milk", "Bread");
        var arrangement = new RowArrangement<Row>();
        arrangement.Start(rows[0]);

        Assert.False(arrangement.DropOn(rows, rows[0]));

        Assert.Equal(["Milk", "Bread"], NamesIn(rows));
    }

    [Fact]
    public void A_drop_with_nothing_being_dragged_changes_nothing()
    {
        var rows = Rows("Milk", "Bread");

        Assert.False(new RowArrangement<Row>().DropOn(rows, rows[1]));

        Assert.Equal(["Milk", "Bread"], NamesIn(rows));
    }

    [Fact]
    public void A_row_that_was_removed_while_being_dragged_is_not_put_back()
    {
        var rows = Rows("Milk", "Bread");
        var dragged = rows[0];
        var arrangement = new RowArrangement<Row>();
        arrangement.Start(dragged);
        rows.Remove(dragged);

        Assert.False(arrangement.DropOn(rows, rows[0]));

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
        var arrangement = new RowArrangement<Row>();
        arrangement.Start(secondMilk);

        Assert.True(arrangement.DropOn(rows, firstMilk));

        Assert.Same(secondMilk, rows[0]);
        Assert.Same(firstMilk, rows[1]);
    }

    [Fact]
    public void Finishing_a_drag_leaves_nothing_being_dragged()
    {
        var rows = Rows("Milk", "Bread");
        var arrangement = new RowArrangement<Row>();
        arrangement.Start(rows[0]);
        Assert.True(arrangement.IsDragging(rows[0]));

        arrangement.Finish();

        Assert.False(arrangement.IsDragging(rows[0]));
        Assert.False(arrangement.DropOn(rows, rows[1]));
    }
}
