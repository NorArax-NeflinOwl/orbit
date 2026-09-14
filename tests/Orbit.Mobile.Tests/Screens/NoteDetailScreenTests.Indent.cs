using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Indenting a line on the phone's note screen. A soft keyboard has no Tab key, so the way in is two
/// buttons over the note's foot beside undo and redo, and a hardware keyboard's Tab reaches the same
/// commands - see NoteLineKeys.
///
/// The edits themselves are the browser's (Orbit.Core.Notes.NoteSurfaceEdits.Indent and Outdent), so a
/// level means the same thing wherever a note is written. The one deliberate difference is where they
/// are taken from: the head of the line rather than the caret, because a button called Indent moves the
/// line rather than typing a tab wherever the caret happens to be.
/// </summary>
public sealed partial class NoteDetailScreenTests
{
    [Fact]
    public async Task Indenting_a_line_puts_a_level_at_its_head()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.IndentCommand.Execute(screen.Lines[0]);

        Assert.Equal("\tmilk", screen.Lines[0].Text);
    }

    /// <summary>Wherever the caret is: the line moves as a line, which is what a button says it will do.</summary>
    [Fact]
    public async Task Indenting_moves_the_line_rather_than_writing_where_the_caret_is()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk and bread");
        var screen = await context.OpenAsync(note.LocalId);

        screen.IndentCommand.Execute(screen.Lines[0]);

        Assert.Equal("\tmilk and bread", screen.Lines[0].Text);
    }

    [Fact]
    public async Task Outdenting_takes_the_level_back_off()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "\tmilk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.OutdentCommand.Execute(screen.Lines[0]);

        Assert.Equal("milk", screen.Lines[0].Text);
    }

    /// <summary>A line written with spaces indents like any other - see NoteSurfaceEdits.SpacesPerIndentation.</summary>
    [Fact]
    public async Task Outdenting_takes_a_level_written_as_spaces_too()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "    milk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.OutdentCommand.Execute(screen.Lines[0]);

        Assert.Equal("milk", screen.Lines[0].Text);
    }

    /// <summary>
    /// A line with nothing to take away is left alone, and the press is not a step to undo: the history
    /// drops an edit that changed no writing, so undo still reaches whatever was done before it.
    /// </summary>
    [Fact]
    public async Task Outdenting_a_line_with_no_indentation_changes_nothing_and_is_not_a_step()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.OutdentCommand.Execute(screen.Lines[0]);

        Assert.Equal("milk", screen.Lines[0].Text);
        Assert.False(screen.CanUndo);
    }

    /// <summary>A level is a step of its own, as every other edit that changes a line's shape is.</summary>
    [Fact]
    public async Task A_level_is_one_step_of_the_history()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.IndentCommand.Execute(screen.Lines[0]);
        Assert.True(screen.CanUndo);

        screen.UndoCommand.Execute(null);
        Assert.Equal("milk", screen.Lines[0].Text);

        screen.RedoCommand.Execute(null);
        Assert.Equal("\tmilk", screen.Lines[0].Text);
    }

    /// <summary>A box keeps its box: a level is about where the line starts, not about what it is.</summary>
    [Fact]
    public async Task Indenting_a_checklist_line_leaves_its_box_alone()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);
        screen.ToggleChecklistCommand.Execute(screen.Lines[0]);

        screen.IndentCommand.Execute(screen.Lines[0]);

        Assert.Equal("\tmilk", screen.Lines[0].Text);
        Assert.True(screen.Lines[0].IsChecklistItem);
    }

    /// <summary>A note shared in to read is not written in, by these or by anything else on the screen.</summary>
    [Fact]
    public async Task A_note_that_cannot_be_changed_indents_nothing()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteSharedToReadAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.IndentCommand.Execute(screen.Lines[0]);

        Assert.Equal("milk", screen.Lines[0].Text);
    }
}
