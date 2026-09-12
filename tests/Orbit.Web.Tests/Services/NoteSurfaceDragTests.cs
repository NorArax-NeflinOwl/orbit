using Orbit.Core.Notes;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Dragging writing about a note - see NoteSurfaceEdits.Drag and Drop. Left to the browser, a drag that
/// spanned lines glued two lines' elements together; worked out here, every line stays a line, and the
/// selection afterwards is the moved writing, which is where checklistTextEditor.js puts it.
/// </summary>
public sealed class NoteSurfaceDragTests
{
    private static NoteContentLine Text(string text) => new(text, IsChecklistItem: false, IsChecked: false);

    private static NoteContentLine Box(string text, bool isChecked = false)
        => new(text, IsChecklistItem: true, IsChecked: isChecked);

    private static SurfaceState Selecting(SurfacePoint anchor, SurfacePoint focus, params NoteContentLine[] lines)
        => new(lines, anchor, focus);

    private static SurfacePoint At(int line, int offset) => new(line, offset);

    [Fact]
    public void Words_dragged_to_another_line_leave_one_and_join_the_other()
    {
        var after = NoteSurfaceEdits.Drag(
            Selecting(At(0, 4), At(0, 9), Text("buy milk today"), Box("eggs")), At(1, 4), copies: false)!;

        Assert.Equal([Text("buy today"), Box("eggsmilk ")], after.Lines);
        Assert.Equal(At(1, 4), after.Anchor);
        Assert.Equal(At(1, 9), after.Focus);
    }

    /// <summary>
    /// The case the browser got wrong: a selection from inside one line to inside the next, dropped
    /// further down. What is left of the two joins into one line, the dragged part-lines arrive as lines,
    /// and nothing ends up with a box in the middle of it.
    /// </summary>
    [Fact]
    public void A_selection_spanning_lines_moves_as_lines()
    {
        var after = NoteSurfaceEdits.Drag(
            Selecting(At(0, 1), At(1, 2), Text("abc"), Box("def"), Text("ghi")), At(2, 3), copies: false)!;

        Assert.Equal([Text("af"), Text("ghibc"), Box("de")], after.Lines);
        Assert.Equal(At(1, 3), after.Anchor);
        Assert.Equal(At(2, 2), after.Focus);
    }

    [Fact]
    public void Dragged_up_the_page_the_lines_above_stay_where_they_are()
    {
        var after = NoteSurfaceEdits.Drag(
            Selecting(At(1, 2), At(2, 1), Text("top"), Text("abcd"), Box("efg")), At(0, 3), copies: false)!;

        Assert.Equal([Text("topcd"), Box("e"), Text("abfg")], after.Lines);
        Assert.Equal(At(0, 3), after.Anchor);
        Assert.Equal(At(1, 1), after.Focus);
    }

    /// <summary>Whole lines go whole - box and tick with them - and land before the line dropped at the head of.</summary>
    [Fact]
    public void Whole_lines_keep_their_boxes_and_ticks_and_land_between_lines()
    {
        var after = NoteSurfaceEdits.Drag(
            Selecting(At(0, 0), At(1, 4), Box("milk", isChecked: true), Box("eggs"), Text("then"), Text("done")),
            At(3, 0), copies: false)!;

        Assert.Equal([Text("then"), Box("milk", isChecked: true), Box("eggs"), Text("done")], after.Lines);
        Assert.Equal(At(1, 0), after.Anchor);
        Assert.Equal(At(2, 4), after.Focus);
    }

    /// <summary>Dropped into the middle of a line, whole lines go after it rather than splitting its words.</summary>
    [Fact]
    public void Whole_lines_dropped_inside_a_line_go_after_it()
    {
        var after = NoteSurfaceEdits.Drag(
            Selecting(At(2, 0), At(3, 0), Text("one"), Text("two"), Box("milk"), Text("end")),
            At(0, 2), copies: false)!;

        Assert.Equal([Text("one"), Box("milk"), Text("two"), Text("end")], after.Lines);
    }

    [Fact]
    public void A_copy_leaves_the_original_where_it_was()
    {
        var after = NoteSurfaceEdits.Drag(
            Selecting(At(0, 0), At(0, 4), Text("milk"), Text("eggs")), At(1, 4), copies: true)!;

        Assert.Equal([Text("milk"), Text("eggsmilk")], after.Lines);
    }

    [Fact]
    public void A_drop_inside_the_selection_or_with_nothing_selected_does_nothing()
    {
        var lines = new[] { Text("abcdef"), Text("ghi") };

        Assert.Null(NoteSurfaceEdits.Drag(Selecting(At(0, 1), At(1, 2), lines), At(0, 4), copies: false));
        Assert.Null(NoteSurfaceEdits.Drag(Selecting(At(0, 1), At(0, 1), lines), At(1, 0), copies: false));
    }

    [Fact]
    public void Text_dropped_from_elsewhere_goes_in_at_the_point_and_is_selected()
    {
        var after = NoteSurfaceEdits.Drop(
            SurfaceState.CaretAt([Text("abc"), Text("end")], At(1, 3)), At(0, 1), "X\nY", readsMarkers: false);

        Assert.Equal([Text("aX"), Text("Ybc"), Text("end")], after.Lines);
        Assert.Equal(At(0, 1), after.Anchor);
        Assert.Equal(At(1, 1), after.Focus);
    }

    /// <summary>A checklist dropped in reads as one pasted does - unless the surface does not read markers.</summary>
    [Fact]
    public void A_dropped_checklist_is_boxes_only_where_markers_are_read()
    {
        var empty = SurfaceState.CaretAt([Text("")], At(0, 0));

        Assert.Equal([Box("milk"), Box("eggs")], NoteSurfaceEdits.Drop(empty, At(0, 0), "- milk\n[] eggs", readsMarkers: true).Lines);
        Assert.Equal([Text("- milk"), Text("[] eggs")], NoteSurfaceEdits.Drop(empty, At(0, 0), "- milk\n[] eggs", readsMarkers: false).Lines);
    }
}
