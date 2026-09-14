using Orbit.Core.Notes;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// What happens to a mark on a stretch of words when the words move - see Orbit.Core.Notes.NoteTextMarks.
/// This is arithmetic, and it is the arithmetic every client depends on: a mark that slid two characters
/// left is a bold that starts in the middle of a word, and nothing about the note would say why.
/// </summary>
public sealed class NoteTextMarkTests
{
    private static NoteTextRun Bold(int start, int length) => new(start, length, NoteTextMark.Bold);

    private static NoteTextRun Italic(int start, int length) => new(start, length, NoteTextMark.Italic);

    [Fact]
    public void Typing_after_the_marked_words_leaves_the_mark_where_it_was()
        => Assert.Equal([Bold(0, 4)], NoteTextMarks.Kept([Bold(0, 4)], at: 14, removed: 0, inserted: 3, textLength: 17));

    [Fact]
    public void Typing_before_the_marked_words_pushes_the_mark_along()
        => Assert.Equal([Bold(2, 4)], NoteTextMarks.Kept([Bold(0, 4)], at: 0, removed: 0, inserted: 2, textLength: 16));

    /// <summary>
    /// A mark sticks to the character before what arrives: typing inside bold words is bold, which is
    /// what every editor does and what the browser reports having drawn.
    /// </summary>
    [Fact]
    public void Typing_inside_the_marked_words_is_marked_too()
        => Assert.Equal([Bold(0, 7)], NoteTextMarks.Kept([Bold(0, 4)], at: 2, removed: 0, inserted: 3, textLength: 17));

    /// <summary>The other side of the same rule: what is typed at the very head of bold words is not bold.</summary>
    [Fact]
    public void Typing_at_the_head_of_the_marked_words_is_not()
        => Assert.Equal([Bold(3, 4)], NoteTextMarks.Kept([Bold(0, 4)], at: 0, removed: 0, inserted: 3, textLength: 17));

    [Fact]
    public void Deleting_the_marked_words_takes_the_mark_with_them()
        => Assert.Empty(NoteTextMarks.Kept([Bold(0, 4)], at: 0, removed: 4, inserted: 0, textLength: 10));

    [Fact]
    public void Deleting_half_the_marked_words_shortens_the_mark()
        => Assert.Equal([Bold(0, 2)], NoteTextMarks.Kept([Bold(0, 4)], at: 2, removed: 2, inserted: 0, textLength: 12));

    [Fact]
    public void A_split_leaves_the_first_half_of_a_mark_behind()
        => Assert.Equal([Bold(0, 2)], NoteTextMarks.Before([Bold(0, 4)], at: 2));

    [Fact]
    public void A_split_carries_the_rest_down_counted_from_the_new_start()
        => Assert.Equal([Bold(1, 3)], NoteTextMarks.After([Bold(6, 3)], at: 5, textLength: 14));

    [Fact]
    public void Joining_two_lines_moves_the_second_line_s_marks_along()
        => Assert.Equal(
            [Bold(0, 4), Italic(4, 2)],
            NoteTextMarks.Joined([Bold(0, 4)], firstLength: 4, [Italic(0, 2)], secondLength: 5));

    /// <summary>
    /// The words were marked, not the gap after them: a bold running to the end of a line does not
    /// spread over the line joined onto it.
    /// </summary>
    [Fact]
    public void A_mark_does_not_spread_across_a_join()
        => Assert.Equal([Bold(0, 4)], NoteTextMarks.Joined([Bold(0, 4)], firstLength: 4, [], secondLength: 5));

    [Fact]
    public void Taking_words_away_takes_their_marks_with_them()
        => Assert.Equal([Italic(0, 5)], NoteTextMarks.Taken([Bold(0, 4), Italic(9, 5)], start: 9, length: 5));

    [Fact]
    public void Touching_stretches_of_one_mark_are_one_stretch()
        => Assert.Equal([Bold(0, 7)], NoteTextMarks.Normalized([Bold(0, 4), Bold(4, 3)], textLength: 10));

    [Fact]
    public void Overlapping_stretches_of_one_mark_are_one_stretch()
        => Assert.Equal([Bold(0, 7)], NoteTextMarks.Normalized([Bold(0, 5), Bold(3, 4)], textLength: 10));

    /// <summary>Bold and italic over the same words are two marks, because one stretch carries one mark.</summary>
    [Fact]
    public void Two_marks_over_the_same_words_stay_two()
        => Assert.Equal(
            [Bold(0, 4), Italic(0, 4)],
            NoteTextMarks.Normalized([Bold(0, 4), Italic(0, 4)], textLength: 10));

    [Fact]
    public void A_mark_running_past_the_words_is_cut_back_to_them()
        => Assert.Equal([Bold(0, 4)], NoteTextMarks.Normalized([Bold(0, 40)], textLength: 4));

    /// <summary>
    /// A mark saved by a newer build reads as None and is dropped rather than drawn wrongly - see
    /// NoteTextMark.None, which is what makes a note from a newer build open here at all.
    /// </summary>
    [Fact]
    public void A_mark_this_build_does_not_know_is_dropped()
    {
        Assert.Equal(NoteTextMark.None, NoteTextMarks.Read("Sparkling"));
        Assert.Empty(NoteTextMarks.Normalized([new NoteTextRun(0, 4, NoteTextMark.None)], textLength: 4));
    }

    [Theory]
    [InlineData("Bold", NoteTextMark.Bold)]
    [InlineData("italic", NoteTextMark.Italic)]
    [InlineData("STRUCKTHROUGH", NoteTextMark.StruckThrough)]
    public void A_mark_is_read_off_its_name_however_it_is_written(string written, NoteTextMark mark)
        => Assert.Equal(mark, NoteTextMarks.Read(written));

    /// <summary>
    /// What something drawing a line is handed: the longest stretches that carry the same marks, which is
    /// not what the runs say directly - two marks over the same words are two runs of their own.
    /// </summary>
    [Fact]
    public void The_words_are_cut_into_the_stretches_that_carry_the_same_marks()
    {
        var pieces = NoteTextMarks.Pieces("milk and bread", [Bold(0, 4), Italic(9, 5)]);

        Assert.Equal(["milk", " and ", "bread"], pieces.Select(piece => piece.Text));
        Assert.Equal([NoteTextMark.Bold], pieces[0].Marks);
        Assert.Empty(pieces[1].Marks);
        Assert.Equal([NoteTextMark.Italic], pieces[2].Marks);
    }

    [Fact]
    public void Words_carrying_two_marks_are_one_stretch_carrying_both()
    {
        var pieces = NoteTextMarks.Pieces("milk", [Bold(0, 4), Italic(0, 4)]);

        Assert.Equal("milk", Assert.Single(pieces).Text);
        Assert.Equal([NoteTextMark.Bold, NoteTextMark.Italic], pieces[0].Marks);
    }

    [Fact]
    public void Nothing_written_is_nothing_to_draw()
        => Assert.Empty(NoteTextMarks.Pieces(string.Empty, [Bold(0, 4)]));

    [Fact]
    public void A_mark_holds_where_every_letter_of_the_stretch_carries_it()
        => Assert.True(NoteTextMarks.Holds([Bold(0, 4)], start: 1, length: 2, NoteTextMark.Bold));

    [Fact]
    public void A_mark_does_not_hold_where_one_letter_lacks_it()
        => Assert.False(NoteTextMarks.Holds([Bold(0, 4)], start: 2, length: 4, NoteTextMark.Bold));

    /// <summary>A caret with nothing selected covers no words, so nothing is marked and the control reads as off.</summary>
    [Fact]
    public void Nothing_selected_holds_nothing()
        => Assert.False(NoteTextMarks.Holds([Bold(0, 4)], start: 2, length: 0, NoteTextMark.Bold));

    [Fact]
    public void Putting_a_mark_on_marks_exactly_those_words()
        => Assert.Equal([Bold(5, 3)], NoteTextMarks.With([], start: 5, length: 3, NoteTextMark.Bold, textLength: 14));

    [Fact]
    public void Taking_a_mark_off_the_middle_leaves_the_two_ends_marked()
        => Assert.Equal(
            [Bold(0, 4), Bold(6, 4)],
            NoteTextMarks.Without([Bold(0, 10)], start: 4, length: 2, NoteTextMark.Bold, textLength: 10));

    [Fact]
    public void Taking_a_mark_off_the_head_leaves_the_rest_of_it()
        => Assert.Equal(
            [Bold(4, 6)],
            NoteTextMarks.Without([Bold(0, 10)], start: 0, length: 4, NoteTextMark.Bold, textLength: 10));

    /// <summary>Pressing Bold says nothing about what is italic.</summary>
    [Fact]
    public void Taking_one_mark_off_leaves_the_others_alone()
        => Assert.Equal(
            [Italic(0, 10)],
            NoteTextMarks.Without([Bold(0, 10), Italic(0, 10)], start: 0, length: 10, NoteTextMark.Bold, textLength: 10));

    /// <summary>Pressing what a stretch already is turns it off - the rule a style follows, and every control anywhere.</summary>
    [Fact]
    public void Pressing_the_mark_a_stretch_already_carries_takes_it_off()
    {
        var on = NoteTextMarks.Marked([], start: 0, length: 4, NoteTextMark.Bold, textLength: 10);
        Assert.Equal([Bold(0, 4)], on);

        Assert.Empty(NoteTextMarks.Marked(on, start: 0, length: 4, NoteTextMark.Bold, textLength: 10));
    }
}
