using Orbit.Mobile.Screens.Notes;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Undo and redo on the phone's note screen. A phone has no Ctrl+Z, so they are two buttons over the
/// note's foot, and they keep the same history the browser's editor keeps - see
/// Orbit.Core.Notes.NoteSurfaceHistory: characters typed one after another are one step, and Enter, a
/// joined line, a tick or a typed "[]" are each a step of their own.
/// </summary>
public sealed partial class NoteDetailScreenTests
{
    /// <summary>What a keyboard does to a field: one character at a time, each a change of its own.</summary>
    private static void Type(NoteLineRow row, string text)
    {
        foreach (var character in text)
        {
            row.Text += character;
        }
    }

    [Fact]
    public async Task A_note_just_opened_has_nothing_to_undo()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        Assert.False(screen.CanUndo);
        Assert.False(screen.CanRedo);
        Assert.False(screen.UndoCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_word_typed_is_undone_as_one_step_and_redone_the_same()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        Type(screen.Lines[0], "bread");
        Assert.True(screen.CanUndo);

        screen.UndoCommand.Execute(null);
        Assert.Equal("milk", screen.Lines[0].Text);
        Assert.False(screen.CanUndo);
        Assert.True(screen.CanRedo);

        screen.RedoCommand.Execute(null);
        Assert.Equal("milkbread", screen.Lines[0].Text);
    }

    /// <summary>A second's pause ends a step, as it does in the browser.</summary>
    [Fact]
    public async Task A_pause_in_the_typing_starts_a_new_step()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", string.Empty);
        var screen = await context.OpenAsync(note.LocalId);

        Type(screen.Lines[0], "egg");
        context.Clock.Advance(TimeSpan.FromSeconds(2));
        Type(screen.Lines[0], "s");

        screen.UndoCommand.Execute(null);
        Assert.Equal("egg", screen.Lines[0].Text);
    }

    /// <summary>
    /// A new line is a step of its own, and undoing it gives the line back its words and the caret back
    /// to where Enter was pressed - which the page is told, since the view model has no fields.
    /// </summary>
    [Fact]
    public async Task Undoing_Enter_joins_the_line_again_and_puts_the_caret_where_it_was_pressed()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milkbread");
        var screen = await context.OpenAsync(note.LocalId);
        var carets = new List<NoteCaret>();
        screen.CaretPlaced += (_, caret) => carets.Add(caret);

        screen.AddLineAfter(screen.Lines[0], caret: 4);
        Assert.Equal(["milk", "bread"], screen.Lines.Select(line => line.Text));

        screen.UndoCommand.Execute(null);

        Assert.Equal(["milkbread"], screen.Lines.Select(line => line.Text));
        var caret = Assert.Single(carets);
        Assert.Same(screen.Lines[0], caret.Line);
        Assert.Equal(4, caret.Offset);
    }

    [Fact]
    public async Task A_line_joined_to_the_one_above_comes_apart_again()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "bread");
        var screen = await context.OpenAsync(note.LocalId);

        screen.MergeIntoTheLineAbove(screen.Lines[1]);
        screen.UndoCommand.Execute(null);

        Assert.Equal(["milk", "bread"], screen.Lines.Select(line => line.Text));

        screen.RedoCommand.Execute(null);
        Assert.Equal(["milkbread"], screen.Lines.Select(line => line.Text));
    }

    /// <summary>
    /// A tick is a step, and undoing it leaves the caret alone: pressing a box never moved it, and putting
    /// it somewhere would open the keyboard over a note somebody was only ticking off.
    /// </summary>
    [Fact]
    public async Task A_tick_is_undone_without_moving_the_caret()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);
        screen.ToggleChecklistCommand.Execute(screen.Lines[0]);
        screen.ToggleCheckedCommand.Execute(screen.Lines[0]);
        var carets = new List<NoteCaret>();
        screen.CaretPlaced += (_, caret) => carets.Add(caret);

        screen.UndoCommand.Execute(null);

        Assert.False(screen.Lines[0].IsChecked);
        Assert.True(screen.Lines[0].IsChecklistItem);
        Assert.Empty(carets);
    }

    /// <summary>As in the browser: the first undo after a typed "[]" gives back the brackets, not the word.</summary>
    [Fact]
    public async Task Undoing_a_typed_box_gives_back_the_brackets_first()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", string.Empty);
        var screen = await context.OpenAsync(note.LocalId);

        Type(screen.Lines[0], "[]");
        Assert.True(screen.Lines[0].IsChecklistItem);

        screen.UndoCommand.Execute(null);

        Assert.False(screen.Lines[0].IsChecklistItem);
        Assert.Equal("[]", screen.Lines[0].Text);
    }

    /// <summary>
    /// The mark is taken out of a field the reader is typing in, so the page is told where the caret
    /// belongs - the field's own caret does not survive its text being rewritten under it.
    /// </summary>
    [Fact]
    public async Task A_typed_box_keeps_the_caret_where_it_was_in_the_words()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);
        var carets = new List<NoteCaret>();
        screen.CaretPlaced += (_, caret) => carets.Add(caret);

        // The caret was at the head of "milk" and "[] " went in front of it.
        screen.Lines[0].Text = "[] milk";

        Assert.Equal("milk", screen.Lines[0].Text);
        Assert.Equal(new NoteCaret(screen.Lines[0], 0), Assert.Single(carets));
    }

    /// <summary>The name is the first line of the writing, so typing in it is undone like any other.</summary>
    [Fact]
    public async Task The_name_is_undone_too()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.Title = "Shopping list";
        screen.UndoCommand.Execute(null);

        Assert.Equal("Shopping", screen.Title);
    }

    /// <summary>Undoing everything is not an edit - leaving then has nothing to ask about.</summary>
    [Fact]
    public async Task Undoing_every_change_leaves_nothing_unsaved()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        Type(screen.Lines[0], " and eggs");
        screen.AddLineAfter(screen.Lines[0]);
        Assert.True(screen.HasUnsavedChanges);

        while (screen.CanUndo)
        {
            screen.UndoCommand.Execute(null);
        }

        Assert.False(screen.HasUnsavedChanges);
        Assert.Equal(["milk"], screen.Lines.Select(line => line.Text));
    }

    /// <summary>
    /// Save writes the note and nothing more, so what was written can still be taken back afterwards -
    /// and taking it back is a change to save again, like any other.
    /// </summary>
    [Fact]
    public async Task Saving_keeps_the_history()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        Type(screen.Lines[0], "s");
        await screen.SaveLinesCommand.ExecuteAsync(null);
        screen.UndoCommand.Execute(null);

        Assert.Equal("milk", screen.Lines[0].Text);
        Assert.True(screen.HasUnsavedChanges);
    }

    /// <summary>A note nobody can change offers no undo, even of what the screen itself did while loading.</summary>
    [Fact]
    public async Task A_note_that_cannot_be_changed_offers_no_undo()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteSharedToReadAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        Assert.True(screen.IsReadOnly);
        Assert.False(screen.CanUndo);
        Assert.False(screen.UndoCommand.CanExecute(null));
    }
}
