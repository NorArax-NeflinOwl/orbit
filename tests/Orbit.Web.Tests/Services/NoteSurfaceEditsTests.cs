using Orbit.Contracts.Notes;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// The edits that change the shape of a note's writing, worked out away from the browser - see
/// NoteSurfaceEdits. The caret each one answers is where checklistTextEditor.js puts it, so a wrong
/// caret here is the caret landing on the wrong line in the note.
/// </summary>
public sealed class NoteSurfaceEditsTests
{
    private static NoteContentLineDto Text(string text) => new(text, IsChecklistItem: false, IsChecked: false);

    private static NoteContentLineDto Box(string text, bool isChecked = false, bool isFailed = false)
        => new(text, IsChecklistItem: true, IsChecked: isChecked, IsFailed: isFailed);

    private static SurfaceState At(int line, int offset, params NoteContentLineDto[] lines)
        => SurfaceState.CaretAt(lines, new SurfacePoint(line, offset));

    private static SurfaceState Selecting(SurfacePoint anchor, SurfacePoint focus, params NoteContentLineDto[] lines)
        => new(lines, anchor, focus);

    [Fact]
    public void Enter_at_the_end_of_a_checklist_line_puts_the_caret_in_the_new_box_rather_than_the_line_after_it()
    {
        var after = NoteSurfaceEdits.Enter(At(1, 4, Text("Shopping"), Box("milk"), Box("eggs")));

        Assert.Equal([Text("Shopping"), Box("milk"), Box(""), Box("eggs")], after.Lines);
        Assert.Equal(new SurfacePoint(2, 0), after.Caret);
        Assert.True(after.IsCollapsed);
    }

    [Fact]
    public void Enter_in_the_middle_of_a_line_carries_the_rest_to_the_new_line_and_the_caret_with_it()
    {
        var after = NoteSurfaceEdits.Enter(At(0, 3, Text("abcdef")));

        Assert.Equal([Text("abc"), Text("def")], after.Lines);
        Assert.Equal(new SurfacePoint(1, 0), after.Caret);
    }

    [Fact]
    public void Enter_on_a_ticked_line_starts_an_unticked_one()
    {
        var after = NoteSurfaceEdits.Enter(At(0, 4, Box("milk", isChecked: true)));

        Assert.Equal([Box("milk", isChecked: true), Box("")], after.Lines);
    }

    [Fact]
    public void Enter_at_the_head_of_a_ticked_line_opens_a_line_above_and_the_tick_stays_with_its_words()
    {
        var after = NoteSurfaceEdits.Enter(At(0, 0, Box("milk", isChecked: true)));

        Assert.Equal([Box(""), Box("milk", isChecked: true)], after.Lines);
        Assert.Equal(new SurfacePoint(1, 0), after.Caret);
    }

    [Fact]
    public void Enter_on_an_empty_checklist_line_leaves_the_list()
    {
        var after = NoteSurfaceEdits.Enter(At(1, 0, Box("milk"), Box("")));

        Assert.Equal([Box("milk"), Text("")], after.Lines);
        Assert.Equal(new SurfacePoint(1, 0), after.Caret);
    }

    [Fact]
    public void Backspace_inside_words_is_left_to_the_browser()
    {
        Assert.Null(NoteSurfaceEdits.Backspace(At(0, 2, Text("abc"))));
    }

    [Fact]
    public void Backspace_at_the_head_of_a_checklist_line_with_words_takes_the_box_and_keeps_the_caret_there()
    {
        var after = NoteSurfaceEdits.Backspace(At(1, 0, Text("Shopping"), Box("milk")))!;

        Assert.Equal([Text("Shopping"), Text("milk")], after.Lines);
        Assert.Equal(new SurfacePoint(1, 0), after.Caret);
    }

    [Fact]
    public void Deleting_an_empty_checklist_line_leaves_the_caret_at_the_end_of_the_line_above()
    {
        var after = NoteSurfaceEdits.Backspace(At(2, 0, Text("Shopping"), Box("milk"), Box(""), Box("eggs")))!;

        Assert.Equal([Text("Shopping"), Box("milk"), Box("eggs")], after.Lines);
        Assert.Equal(new SurfacePoint(1, 4), after.Caret);
    }

    [Fact]
    public void Deleting_the_first_line_when_it_is_an_empty_box_leaves_the_caret_at_the_start_of_the_next()
    {
        var after = NoteSurfaceEdits.Backspace(At(0, 0, Box(""), Box("eggs")))!;

        Assert.Equal([Box("eggs")], after.Lines);
        Assert.Equal(new SurfacePoint(0, 0), after.Caret);
    }

    [Fact]
    public void Deleting_the_only_line_leaves_one_empty_line_to_write_on()
    {
        var after = NoteSurfaceEdits.Backspace(At(0, 0, Box("")))!;

        Assert.Equal([Text("")], after.Lines);
        Assert.Equal(new SurfacePoint(0, 0), after.Caret);
    }

    [Fact]
    public void Backspace_at_the_head_of_a_plain_line_joins_it_to_the_one_above_with_the_caret_at_the_seam()
    {
        var after = NoteSurfaceEdits.Backspace(At(2, 0, Text("a"), Box("milk"), Text(" and eggs")))!;

        Assert.Equal([Text("a"), Box("milk and eggs")], after.Lines);
        Assert.Equal(new SurfacePoint(1, 4), after.Caret);
    }

    [Fact]
    public void Backspace_at_the_very_start_does_nothing_and_is_not_let_through()
    {
        var before = At(0, 0, Text("abc"));

        Assert.Same(before.Lines, NoteSurfaceEdits.Backspace(before)!.Lines);
    }

    [Fact]
    public void Delete_at_the_end_of_a_line_joins_the_next_one_to_it()
    {
        var after = NoteSurfaceEdits.Delete(At(0, 3, Text("abc"), Text("def")))!;

        Assert.Equal([Text("abcdef")], after.Lines);
        Assert.Equal(new SurfacePoint(0, 3), after.Caret);
    }

    [Fact]
    public void Delete_on_an_empty_line_takes_it_away_and_the_next_line_keeps_its_box()
    {
        var after = NoteSurfaceEdits.Delete(At(0, 0, Text(""), Box("eggs")))!;

        Assert.Equal([Box("eggs")], after.Lines);
        Assert.Equal(new SurfacePoint(0, 0), after.Caret);
    }

    [Fact]
    public void Delete_inside_words_is_left_to_the_browser()
    {
        Assert.Null(NoteSurfaceEdits.Delete(At(0, 1, Text("abc"))));
    }

    [Fact]
    public void A_selection_inside_one_line_is_left_to_the_browser()
    {
        Assert.Null(NoteSurfaceEdits.Backspace(Selecting(new(0, 1), new(0, 3), Text("abcdef"))));
    }

    [Fact]
    public void Deleting_whole_checklist_lines_takes_them_box_and_all_and_leaves_the_caret_at_the_end_of_the_line_above()
    {
        // A triple click selects a line from its head to the head of the next one.
        var after = NoteSurfaceEdits.Backspace(Selecting(new(1, 0), new(3, 0), Text("Shopping"), Box("milk"), Box("eggs"), Text("done")))!;

        Assert.Equal([Text("Shopping"), Text("done")], after.Lines);
        Assert.Equal(new SurfacePoint(0, 8), after.Caret);
    }

    [Fact]
    public void Deleting_a_selection_from_the_middle_of_one_line_to_the_middle_of_another_joins_the_ends()
    {
        var after = NoteSurfaceEdits.Delete(Selecting(new(2, 2), new(0, 1), Box("abc"), Text("middle"), Text("xyz")))!;

        Assert.Equal([Box("az")], after.Lines);
        Assert.Equal(new SurfacePoint(0, 1), after.Caret);
    }

    [Fact]
    public void Typing_over_whole_lines_writes_on_a_line_of_its_own_rather_than_at_the_end_of_the_line_above()
    {
        var after = NoteSurfaceEdits.Replace(Selecting(new(1, 0), new(2, 4), Text("Shopping"), Box("milk"), Box("eggs")), "x");

        Assert.Equal([Text("Shopping"), Text("x")], after.Lines);
        Assert.Equal(new SurfacePoint(1, 1), after.Caret);
    }

    [Fact]
    public void Typed_brackets_at_the_head_of_a_line_become_a_box_and_the_caret_stays_in_the_words()
    {
        var after = NoteSurfaceEdits.ReadTypedMarker(At(0, 2, Text("[]milk")))!;

        Assert.Equal([Box("milk")], after.Lines);
        Assert.Equal(new SurfacePoint(0, 0), after.Caret);
    }

    [Fact]
    public void Brackets_anywhere_but_the_head_of_a_line_are_words()
    {
        Assert.Null(NoteSurfaceEdits.ReadTypedMarker(At(0, 6, Text("milk []"))));
    }

    [Fact]
    public void The_toolbar_box_turns_an_empty_line_into_one_and_otherwise_starts_a_line_under_the_caret()
    {
        var onEmpty = NoteSurfaceEdits.StartChecklistItem(At(1, 0, Text("Shopping"), Text("")));
        var onWords = NoteSurfaceEdits.StartChecklistItem(At(0, 3, Text("Shopping"), Text("more")));

        Assert.Equal([Text("Shopping"), Box("")], onEmpty.Lines);
        Assert.Equal(new SurfacePoint(1, 0), onEmpty.Caret);
        Assert.Equal([Text("Shopping"), Box(""), Text("more")], onWords.Lines);
        Assert.Equal(new SurfacePoint(1, 0), onWords.Caret);
    }

    [Fact]
    public void A_press_on_a_box_steps_it_to_its_next_answer_and_leaves_the_caret_where_it_was()
    {
        var before = At(0, 2, Box("milk"), Box("eggs", isChecked: true));

        var once = NoteSurfaceEdits.Cycle(before, 1)!;

        Assert.Equal([Box("milk"), Box("eggs", isFailed: true)], once.Lines);
        Assert.Equal(new SurfacePoint(0, 2), once.Caret);
    }

    [Fact]
    public void A_press_on_a_line_without_a_box_does_nothing()
    {
        Assert.Null(NoteSurfaceEdits.Cycle(At(0, 0, Text("Shopping")), 0));
    }

    [Fact]
    public void A_caret_reported_past_the_end_is_read_as_the_end()
    {
        var after = NoteSurfaceEdits.Enter(At(5, 99, Text("abc")));

        Assert.Equal([Text("abc"), Text("")], after.Lines);
        Assert.Equal(new SurfacePoint(1, 0), after.Caret);
    }
}
