using Orbit.Core.Notes;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// A picture as a kind of line on the note's surface - see NoteContentLine.OfPicture. It shares every
/// guard a table has (NoteSurfaceTableTests) and differs in one place: Backspace or Delete on it takes
/// it away, as any element in a page goes, where a table goes by its own menu.
/// </summary>
public sealed class NoteSurfacePictureTests
{
    private static readonly NotePictureLine APicture = new(Guid.NewGuid(), "image/png", 640, 480);

    private static NoteContentLine Text(string text) => new(text, IsChecklistItem: false, IsChecked: false);

    private static NoteContentLine Picture() => NoteContentLine.OfPicture(APicture);

    private static SurfaceState At(int line, int offset, params NoteContentLine[] lines)
        => SurfaceState.CaretAt(lines, new SurfacePoint(line, offset));

    [Fact]
    public void A_picture_takes_the_place_of_an_empty_line()
    {
        var after = NoteSurfaceEdits.InsertPicture(At(1, 0, Text("Shopping"), Text("")), APicture);

        Assert.Equal(2, after.Lines.Count);
        Assert.True(after.Lines[1].IsAPicture);
        Assert.Equal(APicture, after.Lines[1].Picture);
        Assert.Equal(new SurfacePoint(1, 0), after.Caret);
    }

    /// <summary>
    /// The press in the picture's own corner. Backspace on its line does the same and did it first, but
    /// the caret has to be got onto that line to press it and a picture cannot be typed in - so a reader
    /// who could not land it there had no way to be rid of an attachment at all (2026-09-20).
    /// </summary>
    [Fact]
    public void A_picture_is_taken_out_by_the_press_in_its_corner()
    {
        var after = NoteSurfaceEdits.RemoveElement(At(0, 0, Text("Shopping"), Picture(), Text("Later")), 1)!;

        Assert.Equal([Text("Shopping"), Text("Later")], after.Lines);
    }

    /// <summary>A press on a line that is not a picture or a rule is stale - answered by doing nothing.</summary>
    [Fact]
    public void The_same_press_on_a_line_of_words_does_nothing()
    {
        Assert.Null(NoteSurfaceEdits.RemoveElement(At(0, 0, Text("Shopping"), Picture()), 0));
        Assert.Null(NoteSurfaceEdits.RemoveElement(At(0, 0, Text("Shopping"), Picture()), 7));
    }

    /// <summary>
    /// A note ending in a picture has nowhere for the caret to go, so it could be neither carried on nor
    /// reached from beneath. A press below the last line gives it a line to write on.
    /// </summary>
    [Fact]
    public void A_press_below_a_note_ending_in_a_picture_gives_it_a_line_to_write_on()
    {
        var after = NoteSurfaceEdits.WriteUnderTheEnd(At(1, 0, Text("Shopping"), Picture()))!;

        Assert.Equal([Text("Shopping"), Picture(), Text("")], after.Lines);
        Assert.Equal(new SurfacePoint(2, 0), after.Caret);
    }

    /// <summary>A note that already ends in writing has nothing to add, so the press writes nothing.</summary>
    [Fact]
    public void The_same_press_under_words_adds_no_line()
    {
        Assert.Null(NoteSurfaceEdits.WriteUnderTheEnd(At(0, 8, Text("Shopping"))));
    }

    [Fact]
    public void A_picture_goes_under_a_line_with_words_on_it()
    {
        var after = NoteSurfaceEdits.InsertPicture(At(0, 4, Text("Shopping")), APicture);

        Assert.Equal("Shopping", after.Lines[0].Text);
        Assert.True(after.Lines[1].IsAPicture);
    }

    /// <summary>A picture line carries nothing else: no words, no box, no style - see NoteContentLine.OfPicture.</summary>
    [Fact]
    public void A_picture_line_carries_nothing_else()
    {
        var line = Picture();

        Assert.True(line.IsAPicture);
        Assert.True(line.IsAnElement);
        Assert.Equal(string.Empty, line.Text);
        Assert.False(line.IsChecklistItem);
        Assert.False(line.IsATable);
    }

    [Fact]
    public void Backspace_on_a_picture_takes_it_away()
    {
        var after = NoteSurfaceEdits.Backspace(At(1, 0, Text("milk"), Picture(), Text("eggs")));

        Assert.NotNull(after);
        Assert.Equal([Text("milk"), Text("eggs")], after!.Lines);
    }

    [Fact]
    public void Delete_on_a_picture_takes_it_away_too()
    {
        var after = NoteSurfaceEdits.Delete(At(0, 0, Picture(), Text("eggs")));

        Assert.NotNull(after);
        Assert.Equal([Text("eggs")], after!.Lines);
    }

    [Fact]
    public void Enter_on_a_picture_starts_writing_under_it()
    {
        var after = NoteSurfaceEdits.Enter(At(0, 0, Picture()));

        Assert.True(after.Lines[0].IsAPicture);
        Assert.Equal(Text(""), after.Lines[1]);
        Assert.Equal(new SurfacePoint(1, 0), after.Caret);
    }

    /// <summary>Words never join a picture, from either side - the rule a table has.</summary>
    [Fact]
    public void Words_do_not_join_a_picture()
    {
        var below = At(1, 0, Picture(), Text("milk"));
        var above = At(0, 4, Text("milk"), Picture());

        Assert.Equal(below.Lines, NoteSurfaceEdits.Backspace(below)!.Lines);
        Assert.Equal(above.Lines, NoteSurfaceEdits.Delete(above)!.Lines);
    }

    [Fact]
    public void Writing_pasted_onto_a_picture_lands_under_it()
    {
        var after = NoteSurfaceEdits.Replace(At(0, 0, Picture()), "milk", readsMarkers: false);

        Assert.True(after.Lines[0].IsAPicture);
        Assert.Equal([Text("milk")], after.Lines.Skip(1));
    }

    [Fact]
    public void A_style_and_a_level_leave_a_picture_alone()
    {
        var state = At(0, 0, Picture());

        Assert.Equal(state.Lines, NoteSurfaceEdits.Restyle(state, NoteLineStyle.Heading).Lines);
        Assert.Equal(state.Lines, NoteSurfaceEdits.Indent(state).Lines);
    }

    /// <summary>Two lines naming the same picture are the same line, and a different picture is a different line.</summary>
    [Fact]
    public void Lines_are_compared_by_the_picture_they_name()
    {
        Assert.Equal(Picture(), Picture());
        Assert.NotEqual(Picture(), NoteContentLine.OfPicture(new NotePictureLine(Guid.NewGuid(), "image/png")));
    }
}
