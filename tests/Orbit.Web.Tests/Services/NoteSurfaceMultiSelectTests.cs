using Orbit.Contracts.Notes;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Several boxes selected in a note's writing, and a press on one of them - see NoteSurfaceEdits.Cycle
/// and SelectedChecklistLines. A single press without a selection must still be a single press.
/// </summary>
public sealed class NoteSurfaceMultiSelectTests
{
    private static NoteContentLineDto Text(string text) => new(text, IsChecklistItem: false, IsChecked: false);

    private static NoteContentLineDto Box(string text, bool isChecked = false, bool isFailed = false)
        => new(text, IsChecklistItem: true, IsChecked: isChecked, IsFailed: isFailed);

    private static readonly NoteContentLineDto[] Shopping =
        [Text("Shopping"), Box("milk"), Box("eggs", isChecked: true), Text("then"), Box("bread"), Box("butter")];

    private static SurfaceState Selecting(SurfacePoint anchor, SurfacePoint focus) => new(Shopping, anchor, focus);

    [Fact]
    public void A_press_inside_a_selection_of_boxes_gives_every_one_of_them_the_pressed_box_s_next_answer()
    {
        var selection = Selecting(new SurfacePoint(1, 0), new SurfacePoint(4, 2));

        var after = NoteSurfaceEdits.Cycle(selection, pressedLine: 1)!;

        // milk was unticked, so its next answer is done - and eggs, already done, is done too rather
        // than stepping on to given up.
        Assert.Equal(
            [Text("Shopping"), Box("milk", isChecked: true), Box("eggs", isChecked: true), Text("then"), Box("bread", isChecked: true), Box("butter")],
            after.Lines);
        Assert.Equal(selection.Anchor, after.Anchor);
        Assert.Equal(selection.Focus, after.Focus);
    }

    [Fact]
    public void Pressing_again_carries_on_with_the_same_lines()
    {
        var selection = Selecting(new SurfacePoint(4, 5), new SurfacePoint(1, 0));

        var once = NoteSurfaceEdits.Cycle(selection, pressedLine: 4)!;
        var twice = NoteSurfaceEdits.Cycle(once, pressedLine: 4)!;

        Assert.All([twice.Lines[1], twice.Lines[2], twice.Lines[4]], line => Assert.True(line.IsFailed));
        Assert.False(twice.Lines[5].IsChecked || twice.Lines[5].IsFailed);
    }

    [Fact]
    public void A_box_outside_the_selection_answers_only_for_itself()
    {
        var selection = Selecting(new SurfacePoint(1, 0), new SurfacePoint(2, 4));

        var after = NoteSurfaceEdits.Cycle(selection, pressedLine: 5)!;

        Assert.Equal([Text("Shopping"), Box("milk"), Box("eggs", isChecked: true), Text("then"), Box("bread"), Box("butter", isChecked: true)], after.Lines);
    }

    [Fact]
    public void A_single_press_with_only_a_caret_is_a_single_press()
    {
        var after = NoteSurfaceEdits.Cycle(SurfaceState.CaretAt(Shopping, new SurfacePoint(1, 2)), pressedLine: 1)!;

        Assert.True(after.Lines[1].IsChecked);
        Assert.False(after.Lines[4].IsChecked);
    }

    [Fact]
    public void A_selection_with_one_box_in_it_selects_no_boxes()
    {
        Assert.Empty(NoteSurfaceEdits.SelectedChecklistLines(Selecting(new SurfacePoint(0, 0), new SurfacePoint(1, 2))));
    }

    [Fact]
    public void The_line_a_selection_stops_at_the_head_of_is_not_in_it()
    {
        // Shift+Down from the head of "milk" twice: milk and eggs are selected, "then" is where it stops.
        Assert.Equal([1, 2], NoteSurfaceEdits.SelectedChecklistLines(Selecting(new SurfacePoint(1, 0), new SurfacePoint(3, 0))));
    }

    [Fact]
    public void Only_the_boxes_among_the_selected_lines_are_selected_boxes()
    {
        Assert.Equal([1, 2, 4, 5], NoteSurfaceEdits.SelectedChecklistLines(Selecting(new SurfacePoint(0, 3), new SurfacePoint(5, 6))));
    }
}
