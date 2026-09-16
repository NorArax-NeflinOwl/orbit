using Orbit.Core.Notes;
using Orbit.Mobile.Screens.Notes;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// What a line is, on the phone: a heading, a line of a list, ordinary writing - see
/// Orbit.Core.Notes.NoteLineStyle. The button over the note's foot opens a sheet of the eight, and the
/// press changes the line being written in, as the indent buttons beside it do.
///
/// The edit itself is the browser's (NoteSurfaceEdits.Restyle), so a heading means the same thing
/// wherever a note is written. What is the phone's own, and what is checked here, is that the style
/// survives a save, that a numbered list is numbered over all the lines at once, and that the sheet is
/// worded in the reader's language.
/// </summary>
public sealed partial class NoteDetailScreenTests
{
    [Fact]
    public async Task Asking_for_a_style_gives_the_line_that_style()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.Restyle(screen.Lines[0], NoteLineStyle.Heading);

        Assert.Equal(NoteLineStyle.Heading, screen.Lines[0].Style);
        Assert.True(screen.Lines[0].IsDrawnBold);
    }

    /// <summary>Pressing what a line already is turns it off - the rule every format control follows.</summary>
    [Fact]
    public async Task Asking_for_the_style_a_line_already_is_takes_it_back_to_ordinary_writing()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.Restyle(screen.Lines[0], NoteLineStyle.Heading);
        screen.Restyle(screen.Lines[0], NoteLineStyle.Heading);

        Assert.Equal(NoteLineStyle.Body, screen.Lines[0].Style);
        Assert.False(screen.Lines[0].IsDrawnBold);
    }

    /// <summary>
    /// The line the button was pressed for, and no other - the press moves a line, not the caret's
    /// neighbourhood.
    /// </summary>
    [Fact]
    public async Task Only_the_line_asked_about_changes()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "eggs");
        var screen = await context.OpenAsync(note.LocalId);

        screen.Restyle(screen.Lines[1], NoteLineStyle.Bulleted);

        Assert.Equal(NoteLineStyle.Body, screen.Lines[0].Style);
        Assert.Equal(NoteLineStyle.Bulleted, screen.Lines[1].Style);
    }

    [Fact]
    public async Task A_style_is_still_there_after_the_note_is_saved()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.Restyle(screen.Lines[0], NoteLineStyle.Subheading);
        await screen.SaveLinesCommand.ExecuteAsync(null);

        var stored = (await context.Notes.FindAsync(note.LocalId))!;
        Assert.Equal("Subheading", Assert.Single(stored.Content).Style);
    }

    /// <summary>
    /// A note written in the browser must not be flattened by an edit made here - which is what happened
    /// before the row carried the style: every save wrote the lines back as ordinary writing.
    /// </summary>
    [Fact]
    public async Task Editing_one_line_leaves_another_line_s_style_alone()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "eggs");
        var screen = await context.OpenAsync(note.LocalId);

        screen.Restyle(screen.Lines[0], NoteLineStyle.Heading);
        screen.Lines[1].Text = "eggs and bread";
        await screen.SaveLinesCommand.ExecuteAsync(null);

        var stored = (await context.Notes.FindAsync(note.LocalId))!;
        Assert.Equal(["Heading", "Body"], stored.Content.Select(line => line.Style));
    }

    /// <summary>
    /// The number is worked out over all the lines at once, because a line's number is its place in the
    /// run above it - see NoteLineLook.NumbersFor.
    /// </summary>
    [Fact]
    public async Task A_numbered_list_is_numbered_down_its_run()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "eggs", "and then", "bread");
        var screen = await context.OpenAsync(note.LocalId);

        screen.Restyle(screen.Lines[0], NoteLineStyle.Numbered);
        screen.Restyle(screen.Lines[1], NoteLineStyle.Numbered);
        screen.Restyle(screen.Lines[3], NoteLineStyle.Numbered);

        Assert.Equal("1.", screen.Lines[0].ListMark);
        Assert.Equal("2.", screen.Lines[1].ListMark);
        Assert.Equal(string.Empty, screen.Lines[2].ListMark);
        Assert.Equal("1.", screen.Lines[3].ListMark);
    }

    /// <summary>
    /// A box is what a line is answered in, not what kind of line it is - so a line that has both keeps
    /// the box, and the list's own mark stands down rather than being drawn beside it.
    /// </summary>
    [Fact]
    public async Task A_line_with_a_box_keeps_the_box_and_does_not_draw_a_second_mark()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);
        screen.ToggleChecklistCommand.Execute(screen.Lines[0]);

        screen.Restyle(screen.Lines[0], NoteLineStyle.Bulleted);

        Assert.True(screen.Lines[0].IsChecklistItem);
        Assert.Equal(NoteLineStyle.Bulleted, screen.Lines[0].Style);
        Assert.False(screen.Lines[0].ShowsListMark);
    }

    /// <summary>All eight, and named - the sheet is the only place somebody reads what a style is called.</summary>
    [Fact]
    public async Task The_sheet_offers_every_style_by_name()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        Assert.Equal(
            [
                NoteLineStyle.Title, NoteLineStyle.Heading, NoteLineStyle.Subheading, NoteLineStyle.Body,
                NoteLineStyle.Monospaced, NoteLineStyle.Bulleted, NoteLineStyle.Dashed, NoteLineStyle.Numbered
            ],
            screen.StyleChoices.Select(choice => choice.Style));
        Assert.DoesNotContain(screen.StyleChoices, choice => string.IsNullOrWhiteSpace(choice.Name));
    }

    /// <summary>A style change is a step like any other, so Ctrl+Z - or the button - takes it back.</summary>
    [Fact]
    public async Task Undo_takes_a_style_back_off()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.Restyle(screen.Lines[0], NoteLineStyle.Heading);
        screen.UndoCommand.Execute(null);

        Assert.Equal(NoteLineStyle.Body, screen.Lines[0].Style);
    }
}
