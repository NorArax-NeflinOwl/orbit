using Orbit.Mobile.Screens.Notes;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// A paste into the phone's note screen, read with the browser's rules (Orbit.Core's
/// NoteSurfaceEdits.Replace): several lines become several lines at the caret, and a line the paste
/// starts with "[]", "[ ]" or "- " comes in as a box, "[x]" as a ticked one. A field reports only the
/// text a paste left, so each test sets a line's text the way a paste changes it - all at once.
/// </summary>
public sealed partial class NoteDetailScreenTests
{
    [Fact]
    public async Task Several_lines_pasted_into_a_line_become_several_lines_with_their_boxes()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "bread");
        var screen = await context.OpenAsync(note.LocalId);
        var carets = new List<NoteCaret>();
        screen.CaretPlaced += (_, caret) => carets.Add(caret);

        screen.Lines[0].Text = "milk\n[x] eggs\n- butter";

        Assert.Equal(["milk", "eggs", "butter", "bread"], screen.Lines.Select(line => line.Text));
        Assert.Equal([false, true, true, false], screen.Lines.Select(line => line.IsChecklistItem));
        Assert.Equal([false, true, false, false], screen.Lines.Select(line => line.IsChecked));
        // At the end of what was pasted, which is where carrying on typing carries on.
        Assert.Equal(new NoteCaret(screen.Lines[2], "butter".Length), Assert.Single(carets));
    }

    /// <summary>At the caret, not at the start of the line: what followed the caret ends the last pasted line.</summary>
    [Fact]
    public async Task A_paste_in_the_middle_of_a_line_splits_it_at_the_caret()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milkbread");
        var screen = await context.OpenAsync(note.LocalId);

        screen.Lines[0].Text = "milk and\neggs and bread";

        Assert.Equal(["milk and", "eggs and bread"], screen.Lines.Select(line => line.Text));
    }

    [Theory]
    [InlineData("[x] eggs", true)]
    [InlineData("[X] eggs", true)]
    [InlineData("[] eggs", false)]
    [InlineData("[ ] eggs", false)]
    [InlineData("- eggs", false)]
    public async Task One_line_pasted_into_an_empty_line_comes_in_as_a_box(string pasted, bool ticked)
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", string.Empty);
        var screen = await context.OpenAsync(note.LocalId);

        screen.Lines[0].Text = pasted;

        Assert.True(screen.Lines[0].IsChecklistItem);
        Assert.Equal(ticked, screen.Lines[0].IsChecked);
        Assert.Equal("eggs", screen.Lines[0].Text);
    }

    /// <summary>Only a line the paste starts is read: pasted after words, a mark is words.</summary>
    [Fact]
    public async Task A_mark_pasted_after_words_stays_words()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.Lines[0].Text = "milk - eggs";

        Assert.False(screen.Lines[0].IsChecklistItem);
        Assert.Equal("milk - eggs", screen.Lines[0].Text);
    }

    /// <summary>"-5" is a number, not a bullet - the same rule the browser has.</summary>
    [Fact]
    public async Task A_pasted_negative_number_is_not_a_box()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", string.Empty);
        var screen = await context.OpenAsync(note.LocalId);

        screen.Lines[0].Text = "-5 degrees";

        Assert.False(screen.Lines[0].IsChecklistItem);
        Assert.Equal("-5 degrees", screen.Lines[0].Text);
    }

    /// <summary>
    /// Typing a dash and a space a key at a time is how a line of prose starts as often as a list, so it
    /// stays words - only "[]" makes a box as it is typed, as in the browser.
    /// </summary>
    [Fact]
    public async Task A_dash_typed_a_key_at_a_time_stays_words()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", string.Empty);
        var screen = await context.OpenAsync(note.LocalId);

        Type(screen.Lines[0], "- eggs");

        Assert.False(screen.Lines[0].IsChecklistItem);
        Assert.Equal("- eggs", screen.Lines[0].Text);
    }

    /// <summary>
    /// Enter keeps a list's indentation, so a paste into what it leaves - an indented empty line - starts
    /// that line, and the line keeps its indentation.
    /// </summary>
    [Fact]
    public async Task A_paste_into_an_indented_empty_line_starts_it_and_keeps_the_indentation()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "\t");
        var screen = await context.OpenAsync(note.LocalId);

        screen.Lines[0].Text = "\t[] milk\n[] eggs";

        Assert.Equal(["\tmilk", "eggs"], screen.Lines.Select(line => line.Text));
        Assert.All(screen.Lines, line => Assert.True(line.IsChecklistItem));
    }

    /// <summary>
    /// The name is the first line of the writing: several lines pasted into it leave the first as the
    /// name and the rest as the note's first lines - and the name itself never becomes a box.
    /// </summary>
    [Fact]
    public async Task Several_lines_pasted_into_the_name_go_on_into_the_note()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shop", "bread");
        var screen = await context.OpenAsync(note.LocalId);

        screen.Title = "Shopping\n[] milk";

        Assert.Equal("Shopping", screen.Title);
        Assert.Equal(["milk", "bread"], screen.Lines.Select(line => line.Text));
        Assert.True(screen.Lines[0].IsChecklistItem);
    }

    [Fact]
    public async Task A_paste_is_undone_in_one_step()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.Lines[0].Text = "milk\n- eggs\n- butter";
        screen.UndoCommand.Execute(null);

        Assert.Equal(["milk"], screen.Lines.Select(line => line.Text));
        Assert.False(screen.Lines[0].IsChecklistItem);
        Assert.False(screen.HasUnsavedChanges);
    }

    /// <summary>What a paste made is written by Save like anything else typed - boxes and ticks included.</summary>
    [Fact]
    public async Task A_pasted_checklist_is_saved_as_one()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", string.Empty);
        var screen = await context.OpenAsync(note.LocalId);

        screen.Lines[0].Text = "[x] milk\n[ ] eggs";
        await screen.SaveLinesCommand.ExecuteAsync(null);

        var stored = (await context.Notes.FindAsync(note.LocalId))!.Content;
        Assert.Equal(["milk", "eggs"], stored.Select(line => line.Text));
        Assert.Equal([true, true], stored.Select(line => line.IsChecklistItem));
        Assert.Equal([true, false], stored.Select(line => line.IsChecked));
    }
}
