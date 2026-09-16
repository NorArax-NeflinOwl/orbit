using Orbit.Mobile.Screens.Notes;
using Orbit.Core.Notes;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// A table on the phone's note screen: drawn as a grid of fields, written in through them, reshaped
/// from the table button's sheet, and made by the same button outside one. What the surface itself
/// decides - where a table lands, what taking its last row does, that the keys stay off it - is tested
/// in NoteSurfaceTableTests; here it is that the screen reaches those rules and shows what they said.
/// </summary>
public sealed partial class NoteDetailScreenTests
{
    private static Orbit.Contracts.Notes.NoteContentLineDto ATableLine(params string[][] rows)
        => new(string.Empty, IsChecklistItem: false, IsChecked: false, Table: new Orbit.Contracts.Notes.NoteTableDto(
            [.. rows.Select(row => new Orbit.Contracts.Notes.NoteTableRowDto(
                [.. row.Select(cell => new Orbit.Contracts.Notes.NoteTableCellDto(cell))]))]));

    [Fact]
    public async Task A_table_is_drawn_as_the_grid_of_its_cells()
    {
        using var context = new ScreenContext();
        var note = await context.Notes.CreateAsync("Shopping", [ATableLine(["milk", "2"], ["eggs", "12"])]);
        var screen = await context.OpenAsync(note.LocalId);

        var line = Assert.Single(screen.Lines);
        Assert.True(line.IsATable);
        Assert.False(line.IsOpenForWriting);
        Assert.Equal(["milk", "2"], line.TableRows[0].Select(cell => cell.Text));
        Assert.Equal(["eggs", "12"], line.TableRows[1].Select(cell => cell.Text));
    }

    /// <summary>
    /// Editing the line under a table must not flatten the table - which is what happened to styles and
    /// marks before the row carried them, and would happen here the same way.
    /// </summary>
    [Fact]
    public async Task A_table_survives_an_edit_to_another_line_and_a_save()
    {
        using var context = new ScreenContext();
        var note = await context.Notes.CreateAsync("Shopping", [ATableLine(["milk", "2"]), new("and bread", false, false)]);
        var screen = await context.OpenAsync(note.LocalId);

        screen.Lines[1].Text = "and bread, sliced";
        await screen.SaveLinesCommand.ExecuteAsync(null);

        var stored = (await context.Notes.FindAsync(note.LocalId))!;
        Assert.NotNull(stored.Content[0].Table);
        Assert.Equal("milk", stored.Content[0].Table!.Rows[0].Cells[0].Text);
        Assert.Equal("and bread, sliced", stored.Content[1].Text);
    }

    /// <summary>Enter on the table's own row starts writing under it - the surface's rule, reached from the phone.</summary>
    [Fact]
    public async Task Enter_on_a_table_starts_a_line_under_it()
    {
        using var context = new ScreenContext();
        var note = await context.Notes.CreateAsync("Shopping", [ATableLine(["milk", "2"])]);
        var screen = await context.OpenAsync(note.LocalId);

        var started = screen.AddLineAfter(screen.Lines[0]);

        Assert.NotNull(started);
        Assert.Equal(2, screen.Lines.Count);
        Assert.True(screen.Lines[0].IsATable);
        Assert.False(screen.Lines[1].IsATable);
    }

    /// <summary>What is written in a cell's field is the table's, and goes out with the save.</summary>
    [Fact]
    public async Task Writing_in_a_cell_changes_the_table_and_is_saved()
    {
        using var context = new ScreenContext();
        var note = await context.Notes.CreateAsync("Shopping", [ATableLine(["milk", "2"], ["eggs", "12"])]);
        var screen = await context.OpenAsync(note.LocalId);

        screen.Lines[0].TableRows[1][1].Text = "6";

        Assert.Equal("6", screen.Lines[0].Table!.Rows[1].Cells[1].Text);
        Assert.True(screen.HasUnsavedChanges);
        await screen.SaveLinesCommand.ExecuteAsync(null);

        var stored = (await context.Notes.FindAsync(note.LocalId))!;
        Assert.Equal("6", stored.Content[0].Table!.Rows[1].Cells[1].Text);
    }

    /// <summary>
    /// A cell's marks move along with what is typed, as a line's do, and the field that was written in
    /// is still the field that is there: an undo rewrites its words rather than rebuilding the grid.
    /// </summary>
    [Fact]
    public async Task Typing_in_a_cell_keeps_its_marks_over_the_same_words_and_can_be_undone()
    {
        using var context = new ScreenContext();
        var bold = new Orbit.Contracts.Notes.NoteContentLineDto(string.Empty, false, false, Table: new Orbit.Contracts.Notes.NoteTableDto(
            [new Orbit.Contracts.Notes.NoteTableRowDto([new Orbit.Contracts.Notes.NoteTableCellDto("milk", [new(0, 4, "Bold")])])]));
        var note = await context.Notes.CreateAsync("Shopping", [bold]);
        var screen = await context.OpenAsync(note.LocalId);
        var field = screen.Lines[0].TableRows[0][0];

        field.Text = "2 milk";

        Assert.Equal([new NoteTextRun(2, 4, NoteTextMark.Bold)], screen.Lines[0].Table!.Rows[0].Cells[0].AllMarks);
        Assert.True(screen.CanUndo);

        screen.UndoCommand.Execute(null);

        Assert.Same(field, screen.Lines[0].TableRows[0][0]);
        Assert.Equal("milk", field.Text);
        Assert.Equal([new NoteTextRun(0, 4, NoteTextMark.Bold)], screen.Lines[0].Table!.Rows[0].Cells[0].AllMarks);
    }

    [Fact]
    public async Task A_row_goes_under_the_cell_the_caret_is_in()
    {
        using var context = new ScreenContext();
        var note = await context.Notes.CreateAsync("Shopping", [ATableLine(["milk", "2"], ["eggs", "12"])]);
        var screen = await context.OpenAsync(note.LocalId);

        screen.ReshapeTable(screen.Lines[0].TableRows[0][1], NoteTableAction.AddRowBelow);

        var rows = screen.Lines[0].TableRows;
        Assert.Equal(3, rows.Count);
        Assert.Equal(["milk", "2"], rows[0].Select(cell => cell.Text));
        Assert.Equal(["", ""], rows[1].Select(cell => cell.Text));
        Assert.Equal(["eggs", "12"], rows[2].Select(cell => cell.Text));
        Assert.Equal((2, 0), (rows[2][0].Row, rows[2][0].Column));
    }

    /// <summary>The last column taken away takes the table, and a line to write on is left where it stood.</summary>
    [Fact]
    public async Task Taking_the_last_column_leaves_an_empty_line_to_write_on()
    {
        using var context = new ScreenContext();
        var note = await context.Notes.CreateAsync("Shopping", [ATableLine(["milk"], ["eggs"]), new("and bread", false, false)]);
        var screen = await context.OpenAsync(note.LocalId);

        screen.ReshapeTable(screen.Lines[0].TableRows[1][0], NoteTableAction.RemoveColumn);

        Assert.Equal(2, screen.Lines.Count);
        Assert.False(screen.Lines[0].IsATable);
        Assert.Empty(screen.Lines[0].TableRows);
        Assert.Equal(string.Empty, screen.Lines[0].Text);
        Assert.True(screen.Lines[0].IsOpenForWriting);
        Assert.Equal("and bread", screen.Lines[1].Text);
    }

    /// <summary>The table tool outside a table: an empty line becomes the table, a line with words gets it underneath.</summary>
    [Fact]
    public async Task The_table_tool_puts_a_table_where_the_line_being_written_in_is()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "");
        var screen = await context.OpenAsync(note.LocalId);

        screen.InsertTable(screen.Lines[1]);

        Assert.Equal(2, screen.Lines.Count);
        Assert.True(screen.Lines[1].IsATable);
        Assert.Equal(NoteTables.StartingRows, screen.Lines[1].TableRows.Count);

        screen.InsertTable(screen.Lines[0]);

        Assert.Equal(3, screen.Lines.Count);
        Assert.Equal("milk", screen.Lines[0].Text);
        Assert.True(screen.Lines[1].IsATable);
        Assert.True(screen.Lines[2].IsATable);
    }

    [Fact]
    public async Task The_table_menu_is_worded_for_the_sheet()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        Assert.Equal(
            [NoteTableAction.AddRowBelow, NoteTableAction.AddColumnRight, NoteTableAction.RemoveRow, NoteTableAction.RemoveColumn, NoteTableAction.RemoveTable],
            screen.TableActions.Select(choice => choice.Action));
        Assert.All(screen.TableActions, choice => Assert.False(string.IsNullOrWhiteSpace(choice.Name)));
    }
}
