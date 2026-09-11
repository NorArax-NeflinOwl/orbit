using Orbit.Contracts.Notes;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// A paste into a note's writing - see NoteSurfaceEdits.Replace with readsMarkers. The browser put
/// pasted text at the start of the line; it goes where the caret is now, and a pasted checklist comes in
/// as boxes the way a typed one does.
/// </summary>
public sealed class NoteSurfacePasteTests
{
    private static NoteContentLineDto Text(string text) => new(text, IsChecklistItem: false, IsChecked: false);

    private static NoteContentLineDto Box(string text, bool isChecked = false) => new(text, IsChecklistItem: true, IsChecked: isChecked);

    private static SurfaceState At(int line, int offset, params NoteContentLineDto[] lines)
        => SurfaceState.CaretAt(lines, new SurfacePoint(line, offset));

    private static SurfaceState Paste(SurfaceState state, string text) => NoteSurfaceEdits.Replace(state, text, readsMarkers: true);

    [Fact]
    public void Words_go_in_at_the_caret_rather_than_at_the_start_of_the_line()
    {
        var after = Paste(At(0, 4, Text("Buy today")), "milk ");

        Assert.Equal([Text("Buy milk today")], after.Lines);
        Assert.Equal(new SurfacePoint(0, 9), after.Caret);
    }

    [Fact]
    public void Words_take_the_place_of_a_selection_inside_the_line()
    {
        var after = Paste(new SurfaceState([Text("Buy bread today")], new SurfacePoint(0, 4), new SurfacePoint(0, 9)), "milk");

        Assert.Equal([Text("Buy milk today")], after.Lines);
        Assert.Equal(new SurfacePoint(0, 8), after.Caret);
    }

    [Fact]
    public void Several_lines_become_several_lines_and_the_caret_ends_after_the_last_of_them()
    {
        var after = Paste(At(0, 3, Text("abcdef")), "one\r\ntwo\nthree");

        Assert.Equal([Text("abcone"), Text("two"), Text("threedef")], after.Lines);
        Assert.Equal(new SurfacePoint(2, 5), after.Caret);
    }

    [Fact]
    public void A_copied_checklist_pasted_back_is_a_checklist_again()
    {
        // What onCopy in checklistTextEditor.js writes for two boxes and a line of words.
        var after = Paste(At(1, 0, Text("Shopping"), Text("")), "- milk\n- eggs\nnotes");

        Assert.Equal([Text("Shopping"), Box("milk"), Box("eggs"), Text("notes")], after.Lines);
        Assert.Equal(new SurfacePoint(3, 5), after.Caret);
    }

    [Fact]
    public void Brackets_come_in_as_boxes_and_an_x_in_them_as_a_ticked_one()
    {
        var after = Paste(At(0, 0, Text("")), "[] milk\n[ ] eggs\n[x] bread\n[X] butter");

        Assert.Equal([Box("milk"), Box("eggs"), Box("bread", isChecked: true), Box("butter", isChecked: true)], after.Lines);
    }

    [Fact]
    public void A_marker_pasted_into_the_middle_of_a_line_is_words()
    {
        var after = Paste(At(0, 4, Text("Buy ")), "- milk");

        Assert.Equal([Text("Buy - milk")], after.Lines);
    }

    [Fact]
    public void A_dash_that_is_not_a_bullet_stays_a_dash()
    {
        var after = Paste(At(0, 0, Text("")), "-5 degrees\n--");

        Assert.Equal([Text("-5 degrees"), Text("--")], after.Lines);
    }

    [Fact]
    public void Pasting_onto_a_box_keeps_the_box_and_puts_the_words_in_it()
    {
        var after = Paste(At(0, 0, Box("")), "- milk");

        Assert.Equal([Box("- milk")], after.Lines);
    }

    [Fact]
    public void Typing_over_lines_reads_no_markers()
    {
        var after = NoteSurfaceEdits.Replace(At(0, 0, Text("")), "- milk", readsMarkers: false);

        Assert.Equal([Text("- milk")], after.Lines);
    }
}
