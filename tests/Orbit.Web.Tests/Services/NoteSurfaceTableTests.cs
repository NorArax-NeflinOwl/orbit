using Orbit.Core.Notes;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// A table line on the note's surface. Every edit in NoteSurfaceEdits assumed a line was words with a
/// caret offset in them, and a table has neither - so each of them was given an answer for "the line is
/// a table" before anything drew one, and those answers are what is pinned down here. The table's own
/// edits - a row, a column, the table itself - are here too.
/// </summary>
public sealed class NoteSurfaceTableTests
{
    private static NoteContentLine Text(string text) => new(text, IsChecklistItem: false, IsChecked: false);

    private static NoteContentLine Table(params string[][] rows)
        => NoteContentLine.OfTable(new NoteTable([.. rows.Select(row => new NoteTableRow([.. row.Select(cell => new NoteTableCell(cell))]))]));

    private static SurfaceState At(int line, int offset, params NoteContentLine[] lines)
        => SurfaceState.CaretAt(lines, new SurfacePoint(line, offset));

    private static SurfaceState Selecting(SurfacePoint anchor, SurfacePoint focus, params NoteContentLine[] lines)
        => new(lines, anchor, focus);

    [Fact]
    public void The_table_tool_turns_an_empty_line_into_a_table()
    {
        var after = NoteSurfaceEdits.InsertTable(At(1, 0, Text("Shopping"), Text("")));

        Assert.Equal(2, after.Lines.Count);
        Assert.True(after.Lines[1].IsATable);
        Assert.Equal(2, after.Lines[1].Table!.Columns);
        Assert.Equal(new SurfacePoint(1, 0), after.Caret);
    }

    /// <summary>The rule the tick-box tool follows: a line with words on it keeps them, and the table goes under.</summary>
    [Fact]
    public void The_table_tool_puts_a_table_under_a_line_with_words_on_it()
    {
        var after = NoteSurfaceEdits.InsertTable(At(0, 4, Text("Shopping")));

        Assert.Equal("Shopping", after.Lines[0].Text);
        Assert.True(after.Lines[1].IsATable);
        Assert.Equal(new SurfacePoint(1, 0), after.Caret);
    }

    [Fact]
    public void Enter_on_a_table_starts_writing_under_it()
    {
        var after = NoteSurfaceEdits.Enter(At(0, 0, Table(["a", "b"])));

        Assert.True(after.Lines[0].IsATable);
        Assert.Equal(Text(""), after.Lines[1]);
        Assert.Equal(new SurfacePoint(1, 0), after.Caret);
    }

    /// <summary>A table goes by its own menu, never by a key: a grid of words is not something Backspace quietly takes.</summary>
    [Fact]
    public void Backspace_and_Delete_on_a_table_line_do_nothing()
    {
        var state = At(0, 0, Table(["a", "b"]), Text("milk"));

        Assert.Equal(state.Lines, NoteSurfaceEdits.Backspace(state)!.Lines);
        Assert.Equal(state.Lines, NoteSurfaceEdits.Delete(state)!.Lines);
    }

    [Fact]
    public void Words_do_not_join_a_table_above_them()
    {
        var state = At(1, 0, Table(["a", "b"]), Text("milk"));

        var after = NoteSurfaceEdits.Backspace(state);

        Assert.NotNull(after);
        Assert.Equal(state.Lines, after!.Lines);
        Assert.Equal(new SurfacePoint(1, 0), after.Caret);
    }

    /// <summary>An empty line under a table goes, as an empty line under any line would, and the caret lands on the table.</summary>
    [Fact]
    public void An_empty_line_under_a_table_goes_on_Backspace()
    {
        var after = NoteSurfaceEdits.Backspace(At(1, 0, Table(["a", "b"]), Text("")));

        Assert.NotNull(after);
        Assert.Single(after!.Lines);
        Assert.Equal(new SurfacePoint(0, 0), after.Caret);
    }

    [Fact]
    public void Words_do_not_take_a_table_below_them_on_Delete()
    {
        var state = At(0, 4, Text("milk"), Table(["a", "b"]));

        Assert.Equal(state.Lines, NoteSurfaceEdits.Delete(state)!.Lines);
    }

    /// <summary>A table inside a selection that starts and ends in words goes whole, and the two ends are joined as they would be.</summary>
    [Fact]
    public void A_selection_across_a_table_takes_it_whole()
    {
        var state = Selecting(new SurfacePoint(0, 2), new SurfacePoint(2, 2), Text("milk"), Table(["a", "b"]), Text("eggs"));

        var after = NoteSurfaceEdits.DeleteSelection(state);

        Assert.Equal([Text("migs")], after.Lines);
        Assert.Equal(new SurfacePoint(0, 2), after.Caret);
    }

    /// <summary>
    /// A point inside a table always reads as its head, so a selection that ends on one cannot say how
    /// much of it was meant - and a grid of words is not something to take on a guess. The table stays,
    /// on its own line under what is left.
    /// </summary>
    [Fact]
    public void A_selection_ending_on_a_table_keeps_the_table()
    {
        var state = Selecting(new SurfacePoint(0, 2), new SurfacePoint(1, 0), Text("milk"), Table(["a", "b"]));

        var after = NoteSurfaceEdits.DeleteSelection(state);

        Assert.Equal([Text("mi"), Table(["a", "b"])], after.Lines);
        Assert.Equal(new SurfacePoint(0, 2), after.Caret);
    }

    /// <summary>And the same from the other side: the table stays, and the caret goes to what is left after it.</summary>
    [Fact]
    public void A_selection_starting_on_a_table_keeps_the_table()
    {
        var state = Selecting(new SurfacePoint(0, 0), new SurfacePoint(2, 2), Table(["a", "b"]), Text("milk"), Text("eggs"));

        var after = NoteSurfaceEdits.DeleteSelection(state);

        Assert.Equal([Table(["a", "b"]), Text("gs")], after.Lines);
        Assert.Equal(new SurfacePoint(1, 0), after.Caret);
    }

    [Fact]
    public void Writing_pasted_onto_a_table_lands_under_it()
    {
        var after = NoteSurfaceEdits.Replace(At(0, 0, Table(["a", "b"])), "milk\neggs", readsMarkers: false);

        Assert.True(after.Lines[0].IsATable);
        Assert.Equal([Text("milk"), Text("eggs")], after.Lines.Skip(1));
        Assert.Equal(new SurfacePoint(2, 4), after.Caret);
    }

    [Fact]
    public void Tab_and_a_style_leave_a_table_alone()
    {
        var state = At(0, 0, Table(["a", "b"]));

        Assert.Equal(state.Lines, NoteSurfaceEdits.Indent(state).Lines);
        Assert.Equal(state.Lines, NoteSurfaceEdits.Outdent(state).Lines);
        Assert.Equal(state.Lines, NoteSurfaceEdits.Restyle(state, NoteLineStyle.Heading).Lines);
    }

    /// <summary>The tick-box tool on a table: not a box in place of the table, but a box under it.</summary>
    [Fact]
    public void The_tick_box_tool_on_a_table_starts_a_box_under_it()
    {
        var after = NoteSurfaceEdits.StartChecklistItem(At(0, 0, Table(["a", "b"])));

        Assert.True(after.Lines[0].IsATable);
        Assert.True(after.Lines[1].IsChecklistItem);
    }

    [Fact]
    public void A_row_added_below_the_caret_s_row()
    {
        var after = NoteSurfaceEdits.AddTableRow(At(0, 0, Table(["a", "b"], ["c", "d"])), line: 0, afterRow: 0);

        Assert.NotNull(after);
        Assert.Equal(3, after!.Lines[0].Table!.Rows.Count);
        Assert.Equal("", after.Lines[0].Table!.Rows[1].Cells[0].Text);
    }

    [Fact]
    public void A_column_added_to_the_right_of_the_caret_s_column()
    {
        var after = NoteSurfaceEdits.AddTableColumn(At(0, 0, Table(["a", "b"])), line: 0, afterColumn: 1);

        Assert.NotNull(after);
        Assert.Equal(3, after!.Lines[0].Table!.Columns);
    }

    /// <summary>The last row taken away takes the table, and an empty line to write on is left where it stood.</summary>
    [Fact]
    public void Taking_away_the_last_row_leaves_a_line_to_write_on()
    {
        var after = NoteSurfaceEdits.RemoveTableRow(At(0, 0, Table(["a", "b"])), line: 0, row: 0);

        Assert.NotNull(after);
        Assert.Equal([Text("")], after!.Lines);
        Assert.Equal(new SurfacePoint(0, 0), after.Caret);
    }

    [Fact]
    public void The_table_taken_away_leaves_a_line_to_write_on()
    {
        var after = NoteSurfaceEdits.RemoveTable(At(1, 0, Text("Shopping"), Table(["a", "b"])), line: 1);

        Assert.NotNull(after);
        Assert.Equal([Text("Shopping"), Text("")], after!.Lines);
    }

    /// <summary>A stale press - the line is not a table any more - is answered by doing nothing.</summary>
    [Fact]
    public void A_table_edit_on_a_line_that_is_not_a_table_does_nothing()
    {
        Assert.Null(NoteSurfaceEdits.AddTableRow(At(0, 0, Text("milk")), line: 0, afterRow: 0));
        Assert.Null(NoteSurfaceEdits.RemoveTable(At(0, 0, Text("milk")), line: 7));
    }

    /// <summary>A selection that spans a table and the lines around it can still be marked - the table is simply left out.</summary>
    [Fact]
    public void Marking_across_a_table_marks_the_words_and_leaves_the_table()
    {
        var state = Selecting(new SurfacePoint(0, 0), new SurfacePoint(2, 4), Text("milk"), Table(["a", "b"]), Text("eggs"));

        var after = NoteSurfaceEdits.Mark(state, NoteTextMark.Bold);

        Assert.Equal([new NoteTextRun(0, 4, NoteTextMark.Bold)], after.Lines[0].AllMarks);
        Assert.Empty(after.Lines[1].AllMarks);
        Assert.Equal([new NoteTextRun(0, 4, NoteTextMark.Bold)], after.Lines[2].AllMarks);
    }
}
