using Orbit.Mobile.Screens.Notes;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Where the caret goes after Enter on the phone's note screen. The view model has no fields, so it says
/// where through CaretPlaced and the page puts it there - see NoteDetailPage.OnCaretPlaced.
/// </summary>
public sealed partial class NoteDetailScreenTests
{
    /// <summary>At the end of a line: the new line, and the start of it.</summary>
    [Fact]
    public async Task Enter_puts_the_caret_in_the_new_line()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "bread");
        var screen = await context.OpenAsync(note.LocalId);
        var carets = new List<NoteCaret>();
        screen.CaretPlaced += (_, caret) => carets.Add(caret);

        var added = screen.AddLineAfter(screen.Lines[0]);

        Assert.Same(screen.Lines[1], added);
        Assert.Equal(new NoteCaret(added, 0), Assert.Single(carets));
    }

    /// <summary>
    /// In the middle of an indented line: the words after the caret move down, and the caret goes to the
    /// start of them - after the indentation the new line took from the one above, not before it.
    /// </summary>
    [Fact]
    public async Task Enter_puts_the_caret_after_the_indentation_the_new_line_inherits()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "\tmilkbread");
        var screen = await context.OpenAsync(note.LocalId);
        var carets = new List<NoteCaret>();
        screen.CaretPlaced += (_, caret) => carets.Add(caret);

        var added = screen.AddLineAfter(screen.Lines[0], caret: 5);

        Assert.Equal(["\tmilk", "\tbread"], screen.Lines.Select(line => line.Text));
        Assert.Equal(new NoteCaret(added, 1), Assert.Single(carets));
    }

    /// <summary>
    /// A note with no lines is given one to write in as it is read - which must not put the caret in it,
    /// or opening a note would open the keyboard over it.
    /// </summary>
    [Fact]
    public async Task Reading_an_empty_note_in_does_not_move_the_caret()
    {
        using var context = new ScreenContext();
        var note = await context.Notes.CreateAsync("Empty", []);
        var screen = await context.OpenAsync(note.LocalId);
        var carets = new List<NoteCaret>();
        screen.CaretPlaced += (_, caret) => carets.Add(caret);

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Single(screen.Lines);
        Assert.Empty(carets);
    }

    /// <summary>
    /// Backspace at the head of a line joins it to the one above; where the caret lands is the join -
    /// the end of what the line above said - and the joined line is gone, so there is no field left
    /// holding a caret that has nowhere to be.
    /// </summary>
    [Fact]
    public async Task Joining_a_line_to_the_one_above_lands_where_the_two_meet()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "bread");
        var screen = await context.OpenAsync(note.LocalId);
        var carets = new List<NoteCaret>();
        screen.CaretPlaced += (_, caret) => carets.Add(caret);

        var joined = screen.MergeIntoTheLineAbove(screen.Lines[1]);

        Assert.True(joined);
        Assert.Single(screen.Lines);
        var caret = Assert.Single(carets);
        Assert.Same(screen.Lines[0], caret.Line);
        Assert.Equal("milk".Length, caret.Offset);
    }

    /// <summary>
    /// Enter on an empty box ends the list where it stands - the line becomes a plain one and no line is
    /// started under it, which is the browser's rule (NoteSurfaceEdits.Enter). Nothing that was written
    /// changed, so the caret is left where the reader has it rather than being asked for again: asking
    /// would refocus the field it is already in.
    /// </summary>
    [Fact]
    public async Task Enter_on_an_empty_box_leaves_the_caret_where_it_is()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", string.Empty);
        var screen = await context.OpenAsync(note.LocalId);
        screen.Lines[1].IsChecklistItem = true;
        var carets = new List<NoteCaret>();
        screen.CaretPlaced += (_, caret) => carets.Add(caret);

        var landed = screen.AddLineAfter(screen.Lines[1], caret: 0);

        Assert.Same(screen.Lines[1], landed);
        Assert.Empty(carets);
    }
}
