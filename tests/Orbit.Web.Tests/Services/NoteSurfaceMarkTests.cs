using Orbit.Core.Notes;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Marks on a note's surface: the control that puts one on, and what the edits around it do to the marks
/// already there. The arithmetic itself is NoteTextMarkTests; this is the surface asking for it in the
/// right places, which is where a mark would come back over the wrong words.
/// </summary>
public sealed class NoteSurfaceMarkTests
{
    private static NoteTextRun Bold(int start, int length) => new(start, length, NoteTextMark.Bold);

    private static NoteContentLine Line(string text, params NoteTextRun[] marks)
        => new(text, IsChecklistItem: false, IsChecked: false, IsFailed: false, NoteLineStyle.Body, marks);

    private static SurfaceState At(int line, int offset, params NoteContentLine[] lines)
        => SurfaceState.CaretAt(lines, new SurfacePoint(line, offset));

    private static SurfaceState Selecting(SurfacePoint anchor, SurfacePoint focus, params NoteContentLine[] lines)
        => new(lines, anchor, focus);

    [Fact]
    public void Marking_a_selection_marks_exactly_the_words_it_covers()
    {
        var state = Selecting(new SurfacePoint(0, 0), new SurfacePoint(0, 4), Line("milk and bread"));

        var after = NoteSurfaceEdits.Mark(state, NoteTextMark.Bold);

        Assert.Equal([Bold(0, 4)], after.Lines[0].AllMarks);
    }

    /// <summary>Pressing what the words already are turns it off - the rule a style follows.</summary>
    [Fact]
    public void Marking_words_that_already_carry_the_mark_takes_it_off()
    {
        var state = Selecting(new SurfacePoint(0, 0), new SurfacePoint(0, 4), Line("milk and bread", Bold(0, 4)));

        var after = NoteSurfaceEdits.Mark(state, NoteTextMark.Bold);

        Assert.Empty(after.Lines[0].AllMarks);
    }

    /// <summary>
    /// Across lines the answer is decided once: a selection half bold is one somebody is asking to make
    /// bold, and one that flipped line by line would come back striped.
    /// </summary>
    [Fact]
    public void A_selection_half_marked_is_marked_all_through()
    {
        var state = Selecting(
            new SurfacePoint(0, 0), new SurfacePoint(1, 4),
            Line("milk", Bold(0, 4)), Line("eggs"));

        var after = NoteSurfaceEdits.Mark(state, NoteTextMark.Bold);

        Assert.Equal([Bold(0, 4)], after.Lines[0].AllMarks);
        Assert.Equal([Bold(0, 4)], after.Lines[1].AllMarks);
    }

    /// <summary>A line in the middle of the selection is covered whole; the ends are covered to the caret.</summary>
    [Fact]
    public void Only_the_words_the_selection_covers_are_marked()
    {
        var state = Selecting(
            new SurfacePoint(0, 2), new SurfacePoint(2, 3),
            Line("milk"), Line("eggs"), Line("bread"));

        var after = NoteSurfaceEdits.Mark(state, NoteTextMark.Bold);

        Assert.Equal([Bold(2, 2)], after.Lines[0].AllMarks);
        Assert.Equal([Bold(0, 4)], after.Lines[1].AllMarks);
        Assert.Equal([Bold(0, 3)], after.Lines[2].AllMarks);
    }

    /// <summary>
    /// A caret with nothing selected has no words to mark, and a control that answered it would have to
    /// remember that the next thing typed is bold - which is the browser's business.
    /// </summary>
    [Fact]
    public void A_caret_with_nothing_selected_marks_nothing()
    {
        var state = At(0, 2, Line("milk"));

        var after = NoteSurfaceEdits.Mark(state, NoteTextMark.Bold);

        Assert.Empty(after.Lines[0].AllMarks);
    }

    [Fact]
    public void The_control_is_lit_where_every_word_of_the_selection_carries_the_mark()
    {
        var all = Selecting(new SurfacePoint(0, 0), new SurfacePoint(0, 4), Line("milk and bread", Bold(0, 4)));
        var some = Selecting(new SurfacePoint(0, 0), new SurfacePoint(0, 8), Line("milk and bread", Bold(0, 4)));

        Assert.True(NoteSurfaceEdits.Holds(all, NoteTextMark.Bold));
        Assert.False(NoteSurfaceEdits.Holds(some, NoteTextMark.Bold));
        Assert.False(NoteSurfaceEdits.Holds(At(0, 2, Line("milk", Bold(0, 4))), NoteTextMark.Bold));
    }

    [Fact]
    public void Enter_gives_each_half_of_the_line_the_marks_over_its_own_words()
    {
        var after = NoteSurfaceEdits.Enter(At(0, 5, Line("milk and bread", Bold(0, 4), Bold(9, 5))));

        Assert.Equal([Bold(0, 4)], after.Lines[0].AllMarks);
        Assert.Equal([Bold(4, 5)], after.Lines[1].AllMarks);
    }

    [Fact]
    public void Backspace_joining_two_lines_moves_the_second_line_s_marks_along()
    {
        var after = NoteSurfaceEdits.Backspace(At(1, 0, Line("milk"), Line("eggs", Bold(0, 4))));

        Assert.NotNull(after);
        Assert.Equal("milkeggs", after!.Lines[0].Text);
        Assert.Equal([Bold(4, 4)], after.Lines[0].AllMarks);
    }

    /// <summary>A level of indentation is two characters of writing like any other, so the marks move with the words.</summary>
    [Fact]
    public void Indenting_a_line_moves_its_marks_along_with_the_words()
    {
        var state = Selecting(new SurfacePoint(0, 0), new SurfacePoint(1, 4), Line("milk", Bold(0, 4)), Line("eggs"));

        var after = NoteSurfaceEdits.Indent(state);

        Assert.Equal("\tmilk", after.Lines[0].Text);
        Assert.Equal([Bold(1, 4)], after.Lines[0].AllMarks);
    }

    /// <summary>And back off again, which is the same rule read the other way.</summary>
    [Fact]
    public void Outdenting_a_line_brings_its_marks_back()
    {
        var after = NoteSurfaceEdits.Outdent(At(0, 0, Line("\tmilk", Bold(1, 4))));

        Assert.Equal("milk", after.Lines[0].Text);
        Assert.Equal([Bold(0, 4)], after.Lines[0].AllMarks);
    }

    /// <summary>
    /// The typed "[]" is eaten by the box it makes, so everything after it moves two characters left -
    /// marks included.
    /// </summary>
    [Fact]
    public void A_typed_marker_taken_out_brings_the_marks_back_with_the_words()
    {
        var after = NoteSurfaceEdits.ReadTypedMarker(At(0, 6, Line("[] milk", Bold(3, 4))));

        Assert.NotNull(after);
        Assert.Equal("milk", after!.Lines[0].Text);
        Assert.True(after.Lines[0].IsChecklistItem);
        Assert.Equal([Bold(0, 4)], after.Lines[0].AllMarks);
    }

    /// <summary>
    /// Two lines are the same line when they say the same thing, marks and all - written out on the
    /// record because a list compares by which list it is. The phone tells which lines changed by this,
    /// and the browser's history drops an edit that changed nothing by it.
    /// </summary>
    [Fact]
    public void Two_lines_with_the_same_marks_are_the_same_line()
    {
        Assert.Equal(Line("milk", Bold(0, 4)), Line("milk", Bold(0, 4)));
        Assert.NotEqual(Line("milk", Bold(0, 4)), Line("milk", Bold(0, 2)));
        Assert.NotEqual(Line("milk", Bold(0, 4)), Line("milk"));
        Assert.Equal(Line("milk").GetHashCode(), Line("milk").GetHashCode());
    }
}
