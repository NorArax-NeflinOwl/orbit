using System.Text.Json;
using Orbit.Core.Notes;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// A table as a kind of line - see Orbit.Core.Notes.NoteTable, and the rules in NoteTables. What is
/// pinned down here is the shape: a table is always rectangular and never empty, and the line it is on
/// carries nothing else. The surface's own rules about a table line are NoteSurfaceTableTests.
/// </summary>
public sealed class NoteTableTests
{
    private static NoteTable Of(params string[][] rows)
        => new([.. rows.Select(row => new NoteTableRow([.. row.Select(cell => new NoteTableCell(cell))]))]);

    private static IEnumerable<string> Words(NoteTable table)
        => table.Rows.SelectMany(row => row.Cells.Select(cell => cell.Text));

    [Fact]
    public void A_new_table_is_two_by_two_and_empty()
    {
        var table = NoteTables.Empty();

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(2, table.Columns);
        Assert.True(NoteTables.IsBlank(table));
    }

    [Fact]
    public void A_table_cannot_be_asked_for_with_nothing_in_it()
    {
        var table = NoteTables.Empty(rows: 0, columns: 0);

        Assert.Single(table.Rows);
        Assert.Equal(1, table.Columns);
    }

    /// <summary>Nothing on the wire promises the rows agree; the table is squared up on the way in.</summary>
    [Fact]
    public void Uneven_rows_are_padded_to_the_widest()
    {
        var squared = NoteTables.Squared(Of(["a", "b", "c"], ["d"]));

        Assert.Equal(3, squared.Columns);
        Assert.Equal(["a", "b", "c", "d", "", ""], Words(squared));
    }

    [Fact]
    public void A_row_is_added_under_the_one_named()
    {
        var table = NoteTables.WithRowAfter(Of(["a", "b"], ["c", "d"]), afterRow: 0);

        Assert.Equal(3, table.Rows.Count);
        Assert.Equal(["a", "b", "", "", "c", "d"], Words(table));
    }

    [Fact]
    public void A_column_is_added_to_the_right_of_the_one_named()
    {
        var table = NoteTables.WithColumnAfter(Of(["a", "b"], ["c", "d"]), afterColumn: 0);

        Assert.Equal(3, table.Columns);
        Assert.Equal(["a", "", "b", "c", "", "d"], Words(table));
    }

    [Fact]
    public void A_row_taken_away_takes_its_words()
    {
        var table = NoteTables.WithoutRow(Of(["a", "b"], ["c", "d"]), row: 1);

        Assert.NotNull(table);
        Assert.Equal(["a", "b"], Words(table!));
    }

    [Fact]
    public void A_column_taken_away_takes_its_words_from_every_row()
    {
        var table = NoteTables.WithoutColumn(Of(["a", "b"], ["c", "d"]), column: 0);

        Assert.NotNull(table);
        Assert.Equal(["b", "d"], Words(table!));
    }

    /// <summary>There is no such thing as a table with nothing in it: the last row or column takes the table with it.</summary>
    [Fact]
    public void The_last_row_or_column_taken_away_takes_the_table()
    {
        Assert.Null(NoteTables.WithoutRow(Of(["a", "b"]), row: 0));
        Assert.Null(NoteTables.WithoutColumn(Of(["a"], ["b"]), column: 0));
    }

    [Fact]
    public void A_place_outside_the_table_changes_nothing()
    {
        var table = Of(["a", "b"]);

        Assert.Same(table, NoteTables.WithoutRow(table, row: 5));
        Assert.Same(table, NoteTables.WithoutColumn(table, column: -1));
        Assert.Same(table, NoteTables.WithCell(table, row: 3, column: 0, new NoteTableCell("x")));
    }

    [Fact]
    public void A_cell_s_words_can_be_replaced_marks_and_all()
    {
        var table = NoteTables.WithCell(
            Of(["a", "b"]), row: 0, column: 1, new NoteTableCell("milk", [new NoteTextRun(0, 4, NoteTextMark.Bold)]));

        Assert.Equal("milk", table.Rows[0].Cells[1].Text);
        Assert.Equal([new NoteTextRun(0, 4, NoteTextMark.Bold)], table.Rows[0].Cells[1].AllMarks);
    }

    /// <summary>
    /// Two tables that say the same thing are the same table - written out on the records because a
    /// list compares by which list it is, and a note's lines are compared by what they say.
    /// </summary>
    [Fact]
    public void Two_tables_with_the_same_cells_are_the_same_table()
    {
        Assert.Equal(Of(["a", "b"], ["c", "d"]), Of(["a", "b"], ["c", "d"]));
        Assert.NotEqual(Of(["a", "b"]), Of(["a", "c"]));
        Assert.Equal(NoteContentLine.OfTable(Of(["a"])), NoteContentLine.OfTable(Of(["a"])));
        Assert.NotEqual(NoteContentLine.OfTable(Of(["a"])), NoteContentLine.PlainText(string.Empty));
    }

    /// <summary>A line that carries a table is the table: no words, no box, ordinary style.</summary>
    [Fact]
    public void A_table_line_carries_nothing_else()
    {
        var line = NoteContentLine.OfTable(Of(["a", "b"]));

        Assert.True(line.IsATable);
        Assert.Equal(string.Empty, line.Text);
        Assert.False(line.IsChecklistItem);
        Assert.Equal(NoteLineStyle.Body, line.Style);
    }

    /// <summary>Stored as JSON with the rest of the line, and read back as the same table - see NoteEntity.ContentJson.</summary>
    [Fact]
    public void A_table_line_read_back_is_the_line_that_was_written()
    {
        var line = NoteContentLine.OfTable(Of(["milk", "2"], ["eggs", "12"]));

        var read = JsonSerializer.Deserialize<NoteContentLine>(JsonSerializer.Serialize(line));

        Assert.Equal(line, read);
    }

    [Fact]
    public void A_line_stored_before_tables_existed_is_not_one()
    {
        var read = JsonSerializer.Deserialize<NoteContentLine>(
            """{"Text":"milk","IsChecklistItem":false,"IsChecked":false}""");

        Assert.False(read!.IsATable);
        Assert.Null(read.Table);
    }
}
