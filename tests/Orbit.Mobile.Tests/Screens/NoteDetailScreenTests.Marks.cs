using Orbit.Core.Notes;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Marks on a line's words, on the phone. The phone cannot set one yet and a field cannot draw one, so
/// what matters here is the seam: a marked line is drawn as words (MarkedLabel) until it is being written
/// in, and typing in the field moves the marks along so they are over the same words when the label
/// comes back. The arithmetic is the browser's own (NoteTextMarks.Kept) - this is the phone asking for
/// it at the right moment.
/// </summary>
public sealed partial class NoteDetailScreenTests
{
    private static Orbit.Contracts.Notes.NoteContentLineDto AMarkedLine(string text, int start, int length)
        => new(text, IsChecklistItem: false, IsChecked: false,
            Marks: [new Orbit.Contracts.Notes.NoteTextRunDto(start, length, "Bold")]);

    [Fact]
    public async Task A_line_with_marks_is_drawn_as_words_until_it_is_written_in()
    {
        using var context = new ScreenContext();
        var note = await context.Notes.CreateAsync("Shopping", [AMarkedLine("milk and bread", 0, 4)]);
        var screen = await context.OpenAsync(note.LocalId);

        var line = Assert.Single(screen.Lines);
        Assert.True(line.HasMarks);
        Assert.True(line.ShowsMarkedWords);
        Assert.False(line.IsOpenForWriting);

        line.IsBeingWrittenIn = true;

        Assert.False(line.ShowsMarkedWords);
        Assert.True(line.IsOpenForWriting);
    }

    /// <summary>Typing before the bold word pushes the mark along; the word is still the bold one.</summary>
    [Fact]
    public async Task Typing_moves_the_marks_along_with_the_words()
    {
        using var context = new ScreenContext();
        var note = await context.Notes.CreateAsync("Shopping", [AMarkedLine("milk and bread", 0, 4)]);
        var screen = await context.OpenAsync(note.LocalId);
        var line = screen.Lines[0];
        line.IsBeingWrittenIn = true;

        line.Text = "buy milk and bread";

        Assert.Equal([new NoteTextRun(4, 4, NoteTextMark.Bold)], line.Marks);
    }

    [Fact]
    public async Task Deleting_the_marked_words_takes_the_mark_with_them()
    {
        using var context = new ScreenContext();
        var note = await context.Notes.CreateAsync("Shopping", [AMarkedLine("milk and bread", 0, 4)]);
        var screen = await context.OpenAsync(note.LocalId);
        var line = screen.Lines[0];
        line.IsBeingWrittenIn = true;

        line.Text = " and bread";

        Assert.Empty(line.Marks);
        Assert.False(line.HasMarks);
    }

    /// <summary>What was typed is saved with the marks where they now are, not where they were.</summary>
    [Fact]
    public async Task The_marks_are_saved_where_the_typing_left_them()
    {
        using var context = new ScreenContext();
        var note = await context.Notes.CreateAsync("Shopping", [AMarkedLine("milk and bread", 0, 4)]);
        var screen = await context.OpenAsync(note.LocalId);
        screen.Lines[0].Text = "buy milk and bread";

        await screen.SaveLinesCommand.ExecuteAsync(null);

        var stored = (await context.Notes.FindAsync(note.LocalId))!;
        var run = Assert.Single(stored.Content[0].AllMarks);
        Assert.Equal((4, 4, "Bold"), (run.Start, run.Length, run.Mark));
    }

    /// <summary>A done line is struck through whole, which says more than a bold word inside it.</summary>
    [Fact]
    public async Task A_done_line_is_struck_through_rather_than_drawn_with_its_marks()
    {
        using var context = new ScreenContext();
        var note = await context.Notes.CreateAsync("Shopping", [AMarkedLine("milk", 0, 4)]);
        var screen = await context.OpenAsync(note.LocalId);
        screen.ToggleChecklistCommand.Execute(screen.Lines[0]);
        screen.ToggleCheckedCommand.Execute(screen.Lines[0]);

        Assert.True(screen.Lines[0].IsStruckThrough);
        Assert.False(screen.Lines[0].ShowsMarkedWords);
    }
}
