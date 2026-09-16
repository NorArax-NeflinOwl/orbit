using Orbit.Core.Notes;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// A rule across the note as a kind of line on the surface - see NoteContentLine.OfSeparator. It shares
/// every guard a picture has (NoteSurfacePictureTests), including going on Backspace, and adds one of
/// its own: what is written on it is written once, when the rule is made, and is never worked out again
/// - see NoteSeparatorLine.Stamp.
/// </summary>
public sealed class NoteSurfaceSeparatorTests
{
    private const string Stamp = "Tuesday, 15 September 2026 11:20";

    private static NoteContentLine Text(string text) => new(text, IsChecklistItem: false, IsChecked: false);

    private static NoteContentLine Rule(string stamp = Stamp) => NoteContentLine.OfSeparator(stamp);

    private static SurfaceState At(int line, int offset, params NoteContentLine[] lines)
        => SurfaceState.CaretAt(lines, new SurfacePoint(line, offset));

    [Fact]
    public void A_rule_takes_the_place_of_an_empty_line()
    {
        var after = NoteSurfaceEdits.InsertSeparator(At(1, 0, Text("Shopping"), Text("")), Stamp);

        Assert.Equal(2, after.Lines.Count);
        Assert.True(after.Lines[1].IsASeparator);
        Assert.Equal(Stamp, after.Lines[1].Separator!.Stamp);
        Assert.Equal(new SurfacePoint(1, 0), after.Caret);
    }

    [Fact]
    public void A_rule_goes_under_a_line_with_words_on_it()
    {
        var after = NoteSurfaceEdits.InsertSeparator(At(0, 4, Text("Shopping")), Stamp);

        Assert.Equal("Shopping", after.Lines[0].Text);
        Assert.True(after.Lines[1].IsASeparator);
    }

    /// <summary>A rule with nothing written on it is still a rule, and not "no rule at all".</summary>
    [Fact]
    public void A_plain_rule_is_a_rule()
    {
        var line = Rule(string.Empty);

        Assert.True(line.IsASeparator);
        Assert.False(line.Separator!.HasAStamp);
        Assert.True(Rule().Separator!.HasAStamp);
    }

    /// <summary>A rule carries nothing else: no words, no box, no table - see NoteContentLine.OfSeparator.</summary>
    [Fact]
    public void A_rule_carries_nothing_else()
    {
        var line = Rule();

        Assert.True(line.IsAnElement);
        Assert.True(line.IsTakenAwayByAKey);
        Assert.Equal(string.Empty, line.Text);
        Assert.False(line.IsChecklistItem);
        Assert.False(line.IsATable);
        Assert.False(line.IsAPicture);
    }

    [Fact]
    public void Backspace_on_a_rule_takes_it_away()
    {
        var after = NoteSurfaceEdits.Backspace(At(1, 0, Text("milk"), Rule(), Text("eggs")));

        Assert.NotNull(after);
        Assert.Equal([Text("milk"), Text("eggs")], after!.Lines);
    }

    [Fact]
    public void Delete_on_a_rule_takes_it_away_too()
    {
        var after = NoteSurfaceEdits.Delete(At(0, 0, Rule(), Text("eggs")));

        Assert.NotNull(after);
        Assert.Equal([Text("eggs")], after!.Lines);
    }

    [Fact]
    public void Enter_on_a_rule_starts_writing_under_it()
    {
        var after = NoteSurfaceEdits.Enter(At(0, 0, Rule()));

        Assert.True(after.Lines[0].IsASeparator);
        Assert.Equal(Text(""), after.Lines[1]);
        Assert.Equal(new SurfacePoint(1, 0), after.Caret);
    }

    /// <summary>Words never join a rule, from either side - the rule a table and a picture have.</summary>
    [Fact]
    public void Words_do_not_join_a_rule()
    {
        var below = At(1, 0, Rule(), Text("milk"));
        var above = At(0, 4, Text("milk"), Rule());

        Assert.Equal(below.Lines, NoteSurfaceEdits.Backspace(below)!.Lines);
        Assert.Equal(above.Lines, NoteSurfaceEdits.Delete(above)!.Lines);
    }

    [Fact]
    public void A_style_and_a_level_leave_a_rule_alone()
    {
        var state = At(0, 0, Rule());

        Assert.Equal(state.Lines, NoteSurfaceEdits.Restyle(state, NoteLineStyle.Heading).Lines);
        Assert.Equal(state.Lines, NoteSurfaceEdits.Indent(state).Lines);
    }

    /// <summary>
    /// What is written on the rule is part of the line, so two rules made at different moments are
    /// different lines - which is what lets an undo tell them apart and a history see the change.
    /// </summary>
    [Fact]
    public void Lines_are_compared_by_what_is_written_on_the_rule()
    {
        Assert.Equal(Rule(), Rule());
        Assert.NotEqual(Rule(), Rule("Wednesday, 16 September 2026 09:00"));
        Assert.NotEqual(Rule(), Rule(string.Empty));
    }

    /// <summary>
    /// A rule survives the trip to the wire and back, stamp and all - the shape both clients read and
    /// write (NoteSurfaceLines), and where a dropped field would look like the rule never existing.
    /// </summary>
    [Fact]
    public void A_rule_survives_the_wire()
    {
        var sent = new[] { Text("milk"), Rule(), Text("eggs") }.ToDtos();

        Assert.Equal(Stamp, sent[1].Separator!.Stamp);
        Assert.Equal(string.Empty, sent[1].Text);
        Assert.Null(sent[0].Separator);
        Assert.Equal([Text("milk"), Rule(), Text("eggs")], sent.ToSurfaceLines());
    }

    /// <summary>
    /// The copied words leave a rule out, as they leave a table and a picture out - what is pasted is
    /// read back as lines, and nothing there makes a rule, so a written-out one would come back as
    /// words pretending to be one. See NoteWords.
    /// </summary>
    [Fact]
    public void The_copied_words_leave_a_rule_out()
    {
        var words = NoteWords.Of("Shopping", [Text("milk"), Rule(), Text("eggs")]);

        Assert.Equal("Shopping\nmilk\neggs", words);
    }
}
