using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Several boxes changed with one press on the phone's note screen. The browser chooses them with
/// Shift+click; a phone has no Shift, so "Select boxes" in the note's menu puts a mark beside every box,
/// and a press on one chosen box gives every chosen box that box's next answer - Orbit.Core's
/// NoteSurfaceEdits.Cycle, the rule the browser follows.
/// </summary>
public sealed partial class NoteDetailScreenTests
{
    /// <summary>Two boxes, neither ticked, with a plain line between them.</summary>
    private static async Task<(ScreenContext Context, Orbit.Mobile.Screens.Notes.NoteDetailViewModel Screen)> ABoxedListAsync()
    {
        var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "and then", "eggs");
        var screen = await context.OpenAsync(note.LocalId);
        screen.ToggleChecklistCommand.Execute(screen.Lines[0]);
        screen.ToggleChecklistCommand.Execute(screen.Lines[2]);
        return (context, screen);
    }

    [Fact]
    public async Task Boxes_can_be_chosen_only_where_there_are_two()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "eggs");
        var screen = await context.OpenAsync(note.LocalId);
        screen.ToggleChecklistCommand.Execute(screen.Lines[0]);

        Assert.False(screen.CanPickLines);

        screen.ToggleChecklistCommand.Execute(screen.Lines[1]);
        Assert.True(screen.CanPickLines);
    }

    [Fact]
    public async Task Choosing_boxes_puts_a_mark_beside_every_box_and_nowhere_else()
    {
        var (context, screen) = await ABoxedListAsync();
        using var _ = context;

        screen.StartPickingLinesCommand.Execute(null);

        Assert.True(screen.IsPickingLines);
        Assert.Equal([true, false, true], screen.Lines.Select(line => line.ShowsPickMark));
    }

    [Fact]
    public async Task A_press_on_one_of_two_chosen_boxes_ticks_both()
    {
        var (context, screen) = await ABoxedListAsync();
        using var _ = context;
        screen.StartPickingLinesCommand.Execute(null);
        screen.Lines[0].IsPicked = true;
        screen.Lines[2].IsPicked = true;

        screen.ToggleCheckedCommand.Execute(screen.Lines[2]);

        Assert.True(screen.Lines[0].IsChecked);
        Assert.True(screen.Lines[2].IsChecked);
    }

    /// <summary>The pressed box decides, so a mixed set ends up alike rather than each stepping on.</summary>
    [Fact]
    public async Task A_mixed_set_takes_the_pressed_boxes_next_answer()
    {
        var (context, screen) = await ABoxedListAsync();
        using var _ = context;
        screen.ToggleCheckedCommand.Execute(screen.Lines[0]);
        screen.StartPickingLinesCommand.Execute(null);
        screen.Lines[0].IsPicked = true;
        screen.Lines[2].IsPicked = true;

        // "eggs" is not ticked, so its next answer is done - and "milk", already done, is done too.
        screen.ToggleCheckedCommand.Execute(screen.Lines[2]);
        Assert.Equal([true, true], new[] { screen.Lines[0].IsChecked, screen.Lines[2].IsChecked });

        // Both done now; the next answer is "given up on", for both.
        screen.ToggleCheckedCommand.Execute(screen.Lines[0]);
        Assert.Equal([true, true], new[] { screen.Lines[0].IsFailed, screen.Lines[2].IsFailed });
    }

    /// <summary>A box outside the chosen ones answers only for itself, as in the browser.</summary>
    [Fact]
    public async Task A_box_that_is_not_chosen_answers_only_for_itself()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "bread", "eggs");
        var screen = await context.OpenAsync(note.LocalId);
        foreach (var line in screen.Lines.ToList())
        {
            screen.ToggleChecklistCommand.Execute(line);
        }

        screen.StartPickingLinesCommand.Execute(null);
        screen.Lines[0].IsPicked = true;
        screen.Lines[1].IsPicked = true;

        screen.ToggleCheckedCommand.Execute(screen.Lines[2]);

        Assert.Equal([false, false, true], screen.Lines.Select(line => line.IsChecked));
    }

    /// <summary>One box chosen is just a box: two or more is what makes a press answer for several.</summary>
    [Fact]
    public async Task One_chosen_box_is_a_single_press()
    {
        var (context, screen) = await ABoxedListAsync();
        using var _ = context;
        screen.StartPickingLinesCommand.Execute(null);
        screen.Lines[0].IsPicked = true;

        screen.ToggleCheckedCommand.Execute(screen.Lines[0]);

        Assert.True(screen.Lines[0].IsChecked);
        Assert.False(screen.Lines[2].IsChecked);
    }

    [Fact]
    public async Task A_press_on_several_boxes_is_undone_in_one_step()
    {
        var (context, screen) = await ABoxedListAsync();
        using var _ = context;
        screen.StartPickingLinesCommand.Execute(null);
        screen.Lines[0].IsPicked = true;
        screen.Lines[2].IsPicked = true;
        screen.ToggleCheckedCommand.Execute(screen.Lines[0]);

        screen.UndoCommand.Execute(null);

        Assert.False(screen.Lines[0].IsChecked);
        Assert.False(screen.Lines[2].IsChecked);
    }

    /// <summary>The line over the note says how many are chosen and what a press on one of them does.</summary>
    [Fact]
    public async Task The_screen_says_how_many_boxes_are_chosen()
    {
        var (context, screen) = await ABoxedListAsync();
        using var _ = context;
        screen.StartPickingLinesCommand.Execute(null);

        Assert.Equal("Select the boxes to change together, then press one of them.", screen.PickingHint);

        screen.Lines[0].IsPicked = true;
        screen.Lines[2].IsPicked = true;

        Assert.Equal("2 selected - pressing one of their boxes sets them all.", screen.PickingHint);
    }

    /// <summary>Finishing lets every chosen box go, so a press afterwards is a single press again.</summary>
    [Fact]
    public async Task Finishing_lets_the_chosen_boxes_go()
    {
        var (context, screen) = await ABoxedListAsync();
        using var _ = context;
        screen.StartPickingLinesCommand.Execute(null);
        screen.Lines[0].IsPicked = true;
        screen.Lines[2].IsPicked = true;

        screen.StopPickingLinesCommand.Execute(null);
        screen.ToggleCheckedCommand.Execute(screen.Lines[0]);

        Assert.False(screen.IsPickingLines);
        Assert.All(screen.Lines, line => Assert.False(line.IsPicked || line.ShowsPickMark));
        Assert.False(screen.Lines[2].IsChecked);
    }

    /// <summary>A line that becomes a box while boxes are being chosen can be chosen at once.</summary>
    [Fact]
    public async Task A_line_given_a_box_while_choosing_offers_its_mark()
    {
        var (context, screen) = await ABoxedListAsync();
        using var _ = context;
        screen.StartPickingLinesCommand.Execute(null);

        var added = screen.AddLineAfter(screen.Lines[2]);

        Assert.NotNull(added);
        Assert.True(added.IsChecklistItem);
        Assert.True(added.ShowsPickMark);
    }

    /// <summary>A note nobody can change has no boxes to change together.</summary>
    [Fact]
    public async Task A_note_that_cannot_be_changed_offers_no_choosing()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteSharedToReadAsync("Shopping", "milk", "eggs");
        var screen = await context.OpenAsync(note.LocalId);

        Assert.False(screen.CanPickLines);
        Assert.False(screen.StartPickingLinesCommand.CanExecute(null));
    }
}
