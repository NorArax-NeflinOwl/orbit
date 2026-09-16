using System.Text.Json;
using Orbit.Core.Notes;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// What a line's style means and what changing it does - see NoteLineStyle and
/// NoteSurfaceEdits.Restyle. The rules pinned down here are the ones somebody notices the moment they
/// are wrong: a heading that repeats itself on Enter, a list that cannot be got out of, a number that
/// disagrees with where the line actually is.
/// </summary>
public sealed class NoteLineStyleTests
{
    private static NoteContentLine Text(string text, NoteLineStyle style = NoteLineStyle.Body)
        => new(text, IsChecklistItem: false, IsChecked: false, IsFailed: false, style);

    private static NoteContentLine Box(string text) => new(text, IsChecklistItem: true, IsChecked: false);

    private static SurfaceState At(int line, int offset, params NoteContentLine[] lines)
        => SurfaceState.CaretAt(lines, new SurfacePoint(line, offset));

    [Theory]
    [InlineData(NoteLineStyle.Title)]
    [InlineData(NoteLineStyle.Heading)]
    [InlineData(NoteLineStyle.Subheading)]
    public void A_heading_of_any_size_is_a_heading(NoteLineStyle style) => Assert.True(style.IsAHeading());

    [Theory]
    [InlineData(NoteLineStyle.Body)]
    [InlineData(NoteLineStyle.Monospaced)]
    [InlineData(NoteLineStyle.Bulleted)]
    public void Everything_else_is_not(NoteLineStyle style) => Assert.False(style.IsAHeading());

    [Theory]
    [InlineData(NoteLineStyle.Bulleted)]
    [InlineData(NoteLineStyle.Dashed)]
    [InlineData(NoteLineStyle.Numbered)]
    public void The_three_marked_kinds_are_lists(NoteLineStyle style) => Assert.True(style.IsAList());

    [Theory]
    [InlineData(NoteLineStyle.Body)]
    [InlineData(NoteLineStyle.Title)]
    [InlineData(NoteLineStyle.Monospaced)]
    public void Ordinary_writing_is_not_a_list(NoteLineStyle style) => Assert.False(style.IsAList());

    [Fact]
    public void A_numbered_line_is_numbered_by_its_place_in_the_run_above_it()
    {
        NoteContentLine[] lines =
        [
            Text("Shopping"),
            Text("milk", NoteLineStyle.Numbered),
            Text("eggs", NoteLineStyle.Numbered),
            Text("bread", NoteLineStyle.Numbered)
        ];

        Assert.Equal(1, NoteLineStyles.NumberOf(lines, 1));
        Assert.Equal(2, NoteLineStyles.NumberOf(lines, 2));
        Assert.Equal(3, NoteLineStyles.NumberOf(lines, 3));
    }

    /// <summary>
    /// A line that is not numbered breaks the run, which is what makes two lists with a paragraph
    /// between them two lists rather than one list that counts on through the paragraph.
    /// </summary>
    [Fact]
    public void A_line_that_is_not_numbered_starts_the_counting_again()
    {
        NoteContentLine[] lines =
        [
            Text("one", NoteLineStyle.Numbered),
            Text("two", NoteLineStyle.Numbered),
            Text("and then"),
            Text("one again", NoteLineStyle.Numbered)
        ];

        Assert.Equal(2, NoteLineStyles.NumberOf(lines, 1));
        Assert.Equal(1, NoteLineStyles.NumberOf(lines, 3));
    }

    [Fact]
    public void A_line_that_is_not_numbered_has_no_number_to_draw()
    {
        NoteContentLine[] lines = [Text("milk", NoteLineStyle.Bulleted), Box("eggs")];

        Assert.Equal(0, NoteLineStyles.NumberOf(lines, 0));
        Assert.Equal(0, NoteLineStyles.NumberOf(lines, 1));
        Assert.Equal(0, NoteLineStyles.NumberOf(lines, -1));
        Assert.Equal(0, NoteLineStyles.NumberOf(lines, 7));
    }

    [Theory]
    [InlineData("Heading", NoteLineStyle.Heading)]
    [InlineData("heading", NoteLineStyle.Heading)]
    [InlineData("NUMBERED", NoteLineStyle.Numbered)]
    public void A_style_is_read_off_its_name_however_it_is_written(string written, NoteLineStyle style)
        => Assert.Equal(style, NoteLineStyles.Read(written));

    /// <summary>
    /// A note saved by a newer build must still open on an older one, drawn plainly, rather than
    /// refusing to load - the rule every stored-by-name enum in Orbit follows.
    /// </summary>
    [Theory]
    [InlineData("Quoted")]
    [InlineData("")]
    [InlineData(null)]
    public void A_style_this_build_does_not_know_is_ordinary_writing(string? written)
        => Assert.Equal(NoteLineStyle.Body, NoteLineStyles.Read(written));

    /// <summary>
    /// A note's lines are stored as JSON - see NoteEntity.ContentJson and the phone's own store - so the
    /// style is written as a word. A number there would mean the order of the enum decided what an old
    /// note says.
    /// </summary>
    [Fact]
    public void A_style_is_stored_as_a_word_rather_than_a_number()
    {
        var written = JsonSerializer.Serialize(
            new NoteContentLine("Shopping", IsChecklistItem: false, IsChecked: false, IsFailed: false, NoteLineStyle.Heading));

        Assert.Contains("\"Heading\"", written);
    }

    [Fact]
    public void A_line_read_back_is_the_line_that_was_written()
    {
        var line = new NoteContentLine("milk", IsChecklistItem: true, IsChecked: true, IsFailed: false, NoteLineStyle.Numbered);

        var read = JsonSerializer.Deserialize<NoteContentLine>(JsonSerializer.Serialize(line));

        Assert.Equal(line, read);
    }

    /// <summary>A line saved before styles existed says nothing about one, and reads as ordinary writing.</summary>
    [Fact]
    public void A_line_stored_before_styles_existed_reads_as_ordinary_writing()
    {
        var read = JsonSerializer.Deserialize<NoteContentLine>(
            """{"Text":"milk","IsChecklistItem":true,"IsChecked":false,"IsFailed":false}""");

        Assert.Equal(NoteLineStyle.Body, read!.Style);
    }

    [Fact]
    public void Asking_for_a_style_gives_the_line_that_style()
    {
        var after = NoteSurfaceEdits.Restyle(At(0, 3, Text("Shopping")), NoteLineStyle.Heading);

        Assert.Equal([Text("Shopping", NoteLineStyle.Heading)], after.Lines);
    }

    /// <summary>
    /// Pressing what a line already is turns it off, the way a format control works everywhere - and
    /// the only way somebody gets out of a style without knowing that "Body" is the name of not one.
    /// </summary>
    [Fact]
    public void Asking_for_the_style_a_line_already_is_takes_it_back_to_ordinary_writing()
    {
        var heading = At(0, 0, Text("Shopping", NoteLineStyle.Heading));

        var after = NoteSurfaceEdits.Restyle(heading, NoteLineStyle.Heading);

        Assert.Equal([Text("Shopping")], after.Lines);
    }

    [Fact]
    public void A_selection_takes_the_style_across_every_line_it_touches()
    {
        var selection = new SurfaceState(
            [Text("milk"), Text("eggs"), Text("bread")],
            new SurfacePoint(0, 2),
            new SurfacePoint(2, 1));

        var after = NoteSurfaceEdits.Restyle(selection, NoteLineStyle.Bulleted);

        Assert.Equal(
            [
                Text("milk", NoteLineStyle.Bulleted),
                Text("eggs", NoteLineStyle.Bulleted),
                Text("bread", NoteLineStyle.Bulleted)
            ],
            after.Lines);
    }

    /// <summary>
    /// Only where all of them are already it. A selection with one line of something else in it is a
    /// selection somebody is asking to make the same, not one they are asking to turn off.
    /// </summary>
    [Fact]
    public void A_selection_only_half_in_the_style_is_made_all_of_it()
    {
        var selection = new SurfaceState(
            [Text("milk", NoteLineStyle.Bulleted), Text("eggs")],
            new SurfacePoint(0, 0),
            new SurfacePoint(1, 4));

        var after = NoteSurfaceEdits.Restyle(selection, NoteLineStyle.Bulleted);

        Assert.Equal(
            [Text("milk", NoteLineStyle.Bulleted), Text("eggs", NoteLineStyle.Bulleted)],
            after.Lines);
    }

    /// <summary>Restyling says nothing about the words, so what was selected stays selected.</summary>
    [Fact]
    public void What_was_selected_is_still_selected_afterwards()
    {
        var selection = new SurfaceState(
            [Text("milk"), Text("eggs")],
            new SurfacePoint(0, 1),
            new SurfacePoint(1, 3));

        var after = NoteSurfaceEdits.Restyle(selection, NoteLineStyle.Dashed);

        Assert.Equal(new SurfacePoint(0, 1), after.Anchor);
        Assert.Equal(new SurfacePoint(1, 3), after.Focus);
    }

    /// <summary>A style is the line's, not the tick's - a box that is also a bullet keeps its box.</summary>
    [Fact]
    public void A_tick_box_keeps_its_box_when_it_is_given_a_style()
    {
        var after = NoteSurfaceEdits.Restyle(At(0, 0, Box("milk")), NoteLineStyle.Numbered);

        Assert.True(after.Lines[0].IsChecklistItem);
        Assert.Equal(NoteLineStyle.Numbered, after.Lines[0].Style);
    }

    [Theory]
    [InlineData(NoteLineStyle.Bulleted)]
    [InlineData(NoteLineStyle.Dashed)]
    [InlineData(NoteLineStyle.Numbered)]
    public void Enter_on_a_line_of_a_list_starts_another_line_of_the_same_list(NoteLineStyle style)
    {
        var after = NoteSurfaceEdits.Enter(At(0, 4, Text("milk", style)));

        Assert.Equal([Text("milk", style), Text("", style)], after.Lines);
        Assert.Equal(new SurfacePoint(1, 0), after.Caret);
    }

    /// <summary>
    /// The same rule a tick box has had since 2026-09-12: Enter on an empty one ends the list rather
    /// than piling up empty bullets.
    /// </summary>
    [Theory]
    [InlineData(NoteLineStyle.Bulleted)]
    [InlineData(NoteLineStyle.Dashed)]
    [InlineData(NoteLineStyle.Numbered)]
    public void Enter_on_an_empty_line_of_a_list_ends_the_list(NoteLineStyle style)
    {
        var after = NoteSurfaceEdits.Enter(At(1, 0, Text("milk", style), Text("", style)));

        Assert.Equal([Text("milk", style), Text("")], after.Lines);
        Assert.Equal(new SurfacePoint(1, 0), after.Caret);
    }

    [Theory]
    [InlineData(NoteLineStyle.Title)]
    [InlineData(NoteLineStyle.Heading)]
    [InlineData(NoteLineStyle.Subheading)]
    public void Enter_after_a_heading_starts_ordinary_writing(NoteLineStyle style)
    {
        var after = NoteSurfaceEdits.Enter(At(0, 8, Text("Shopping", style)));

        Assert.Equal([Text("Shopping", style), Text("")], after.Lines);
    }

    /// <summary>
    /// Splitting a heading in the middle is the one case where the words that move down are not a
    /// heading: Enter after a heading is ordinary writing whether or not there was anything left on
    /// the line.
    /// </summary>
    [Fact]
    public void Splitting_a_heading_leaves_the_heading_above_and_ordinary_writing_below()
    {
        var after = NoteSurfaceEdits.Enter(At(0, 4, Text("Shopping", NoteLineStyle.Heading)));

        Assert.Equal([Text("Shop", NoteLineStyle.Heading), Text("ping")], after.Lines);
    }

    /// <summary>Monospaced is not a heading and not a list, so Enter simply carries it on.</summary>
    [Fact]
    public void Enter_on_a_monospaced_line_stays_monospaced()
    {
        var after = NoteSurfaceEdits.Enter(At(0, 3, Text("git", NoteLineStyle.Monospaced)));

        Assert.Equal(
            [Text("git", NoteLineStyle.Monospaced), Text("", NoteLineStyle.Monospaced)],
            after.Lines);
    }
}
