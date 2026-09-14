using Orbit.Core.Notes;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// A table on the phone's note screen. The phone does not make one and cannot write in its cells yet -
/// see info/future-plan.md - so what matters here is that a table written in the browser is drawn as
/// its grid, is carried through every edit unchanged, and comes back out of a save exactly as it went
/// in. The rule that keeps the keys off it is the surface's own, tested in NoteSurfaceTableTests.
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
        Assert.Equal(["milk", "2"], line.TableRows[0]);
        Assert.Equal(["eggs", "12"], line.TableRows[1]);
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
}
