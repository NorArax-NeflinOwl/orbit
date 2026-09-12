using Orbit.Core.Notes;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Tab and Shift+Tab in a note's writing - see NoteSurfaceEdits.Indent and Outdent. One level is a tab
/// character stored in the line's text (NoteSurfaceEdits.Indentation), so these read the text itself.
/// </summary>
public sealed class NoteSurfaceIndentTests
{
    private static NoteContentLine Text(string text) => new(text, IsChecklistItem: false, IsChecked: false);

    private static NoteContentLine Box(string text) => new(text, IsChecklistItem: true, IsChecked: false);

    private static SurfaceState At(int line, int offset, params NoteContentLine[] lines)
        => SurfaceState.CaretAt(lines, new SurfacePoint(line, offset));

    [Fact]
    public void One_level_is_a_tab_character()
    {
        Assert.Equal("\t", NoteSurfaceEdits.Indentation);
    }

    [Fact]
    public void Tab_puts_a_level_in_at_the_caret_and_the_caret_after_it()
    {
        var after = NoteSurfaceEdits.Indent(At(0, 3, Text("abcdef")));

        Assert.Equal([Text("abc\tdef")], after.Lines);
        Assert.Equal(new SurfacePoint(0, 4), after.Caret);
    }

    [Fact]
    public void Tab_takes_the_place_of_a_selection_inside_one_line()
    {
        var after = NoteSurfaceEdits.Indent(new SurfaceState([Text("abcdef")], new SurfacePoint(0, 1), new SurfacePoint(0, 4)));

        Assert.Equal([Text("a\tef")], after.Lines);
        Assert.Equal(new SurfacePoint(0, 2), after.Caret);
    }

    [Fact]
    public void Tab_on_a_box_indents_its_words_and_keeps_the_box()
    {
        var after = NoteSurfaceEdits.Indent(At(0, 0, Box("milk")));

        Assert.Equal([Box("\tmilk")], after.Lines);
    }

    [Fact]
    public void Tab_over_several_lines_indents_each_at_its_start_and_keeps_them_selected()
    {
        var before = new SurfaceState([Text("one"), Box("two"), Text("three")], new SurfacePoint(0, 1), new SurfacePoint(1, 2));

        var after = NoteSurfaceEdits.Indent(before);

        Assert.Equal([Text("\tone"), Box("\ttwo"), Text("three")], after.Lines);
        Assert.Equal(new SurfacePoint(0, 2), after.Anchor);
        Assert.Equal(new SurfacePoint(1, 3), after.Focus);
    }

    [Fact]
    public void A_selection_ending_at_the_head_of_a_line_does_not_indent_that_line()
    {
        var before = new SurfaceState([Text("one"), Text("two"), Text("three")], new SurfacePoint(0, 0), new SurfacePoint(2, 0));

        var after = NoteSurfaceEdits.Indent(before);

        Assert.Equal([Text("\tone"), Text("\ttwo"), Text("three")], after.Lines);
        Assert.Equal(new SurfacePoint(0, 0), after.Anchor);
        Assert.Equal(new SurfacePoint(2, 0), after.Focus);
    }

    [Fact]
    public void Shift_Tab_takes_one_level_from_the_start_of_the_line_wherever_the_caret_is()
    {
        var after = NoteSurfaceEdits.Outdent(At(0, 5, Text("\t\tabcdef")));

        Assert.Equal([Text("\tabcdef")], after.Lines);
        Assert.Equal(new SurfacePoint(0, 4), after.Caret);
    }

    [Fact]
    public void Shift_Tab_takes_up_to_four_spaces_from_a_line_indented_with_spaces()
    {
        var after = NoteSurfaceEdits.Outdent(At(0, 6, Text("      abc")));

        Assert.Equal([Text("  abc")], after.Lines);
        Assert.Equal(new SurfacePoint(0, 2), after.Caret);
    }

    [Fact]
    public void Shift_Tab_on_a_line_with_no_indentation_leaves_it_alone()
    {
        var after = NoteSurfaceEdits.Outdent(At(0, 2, Text("abc")));

        Assert.Equal([Text("abc")], after.Lines);
        Assert.Equal(new SurfacePoint(0, 2), after.Caret);
    }

    [Fact]
    public void Shift_Tab_over_several_lines_outdents_each_of_them()
    {
        var before = new SurfaceState([Text("\tone"), Box("  two"), Text("\tthree")], new SurfacePoint(0, 0), new SurfacePoint(1, 5));

        var after = NoteSurfaceEdits.Outdent(before);

        Assert.Equal([Text("one"), Box("two"), Text("\tthree")], after.Lines);
        Assert.Equal(new SurfacePoint(0, 0), after.Anchor);
        Assert.Equal(new SurfacePoint(1, 3), after.Focus);
    }

    [Fact]
    public void A_caret_inside_the_indentation_Shift_Tab_takes_ends_at_the_start_of_the_line()
    {
        var after = NoteSurfaceEdits.Outdent(At(0, 1, Text("    abc")));

        Assert.Equal([Text("abc")], after.Lines);
        Assert.Equal(new SurfacePoint(0, 0), after.Caret);
    }
}
