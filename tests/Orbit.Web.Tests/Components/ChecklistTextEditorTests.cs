using System.Text.Json;
using Bunit;
using Orbit.Contracts.Notes;
using Orbit.Web.Components;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// The bridge checklistTextEditor.js calls for every edit that changes the shape of the lines - see
/// ChecklistTextEditor.Edit. bUnit cannot type, so these call it the way the browser does, with the
/// JSON the browser sends, and read the answer the browser would draw: the lines, and where the caret
/// is put.
/// </summary>
public sealed class ChecklistTextEditorTests : OrbitTestContext
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static NoteContentLineDto Text(string text) => new(text, IsChecklistItem: false, IsChecked: false);

    private static NoteContentLineDto Box(string text) => new(text, IsChecklistItem: true, IsChecked: false);

    private IRenderedComponent<ChecklistTextEditor> Surface(params NoteContentLineDto[] lines)
        => RenderComponent<ChecklistTextEditor>(parameters => parameters.Add(editor => editor.Lines, lines));

    private static string? Send(IRenderedComponent<ChecklistTextEditor> cut, object request)
        => cut.InvokeAsync(() => cut.Instance.Edit(JsonSerializer.Serialize(request, Json))).GetAwaiter().GetResult();

    private static object Caret(int line, int offset) => new { line, offset };

    [Fact]
    public void Enter_answers_the_new_line_as_where_the_caret_goes()
    {
        var cut = Surface(Text("Shopping"), Box("milk"));
        var lines = new[] { Text("Shopping"), Box("milk") };

        var answer = Send(cut, new { command = "enter", lines, anchor = Caret(1, 4), focus = Caret(1, 4) });

        using var document = JsonDocument.Parse(answer!);
        Assert.Equal(2, document.RootElement.GetProperty("focus").GetProperty("line").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("focus").GetProperty("offset").GetInt32());
        Assert.Equal(3, document.RootElement.GetProperty("lines").GetArrayLength());
        Assert.Equal([Text("Shopping"), Box("milk"), Box("")], cut.Instance.Lines);
    }

    [Fact]
    public void A_key_the_browser_should_handle_itself_is_answered_with_nothing()
    {
        var cut = Surface(Text("abc"));

        var answer = Send(cut, new { command = "backspace", lines = new[] { Text("abc") }, anchor = Caret(0, 2), focus = Caret(0, 2) });

        Assert.Null(answer);
    }

    [Fact]
    public void Typing_brackets_at_the_head_of_a_line_answers_a_box_and_ordinary_typing_nothing()
    {
        var cut = Surface(Text(""));

        var plain = Send(cut, new { command = "typed", lines = new[] { Text("m") }, anchor = Caret(0, 1), focus = Caret(0, 1), inputType = "insertText", text = "m" });
        var marker = Send(cut, new { command = "typed", lines = new[] { Text("[]") }, anchor = Caret(0, 2), focus = Caret(0, 2), inputType = "insertText", text = "]" });

        Assert.Null(plain);
        Assert.NotNull(marker);
        Assert.Equal([Box("")], cut.Instance.Lines);
    }

    [Fact]
    public void A_character_still_being_composed_is_not_read_as_a_marker()
    {
        var cut = Surface(Text(""));

        var answer = Send(cut, new { command = "typed", lines = new[] { Text("[]") }, anchor = Caret(0, 2), focus = Caret(0, 2), inputType = "insertText", text = "]", composing = true });

        Assert.Null(answer);
    }

    [Fact]
    public void A_paste_goes_in_at_the_caret_and_a_copied_checklist_comes_back_as_boxes()
    {
        var lines = new[] { Text("Buy today") };
        var cut = Surface(lines);

        var words = Send(cut, new { command = "paste", lines, anchor = Caret(0, 4), focus = Caret(0, 4), text = "milk " });
        var checklist = Send(cut, new { command = "paste", lines = new[] { Text("") }, anchor = Caret(0, 0), focus = Caret(0, 0), text = "- milk\n- eggs" });

        using var answer = JsonDocument.Parse(words!);
        Assert.Equal(9, answer.RootElement.GetProperty("focus").GetProperty("offset").GetInt32());
        Assert.NotNull(checklist);
        Assert.Equal([Box("milk"), Box("eggs")], cut.Instance.Lines);
    }

    [Fact]
    public void Tab_is_taken_only_by_a_surface_that_asks_for_it()
    {
        var lines = new[] { Text("abc") };
        var field = Surface(lines);
        var page = RenderComponent<ChecklistTextEditor>(parameters => parameters
            .Add(editor => editor.Lines, lines)
            .Add(editor => editor.TakesTab, true));
        var request = new { command = "indent", lines, anchor = Caret(0, 0), focus = Caret(0, 0) };

        Assert.Null(Send(field, request));
        Assert.NotNull(Send(page, request));
        Assert.Equal([Text("\tabc")], page.Instance.Lines);
    }

    private static SurfacePointAnswer FocusOf(string answer)
    {
        using var document = JsonDocument.Parse(answer);
        var focus = document.RootElement.GetProperty("focus");
        return new SurfacePointAnswer(focus.GetProperty("line").GetInt32(), focus.GetProperty("offset").GetInt32());
    }

    private sealed record SurfacePointAnswer(int Line, int Offset);

    [Fact]
    public void Undo_takes_the_last_step_back_with_the_caret_where_it_began_and_redo_puts_it_again()
    {
        var lines = new[] { Text("Shopping"), Box("milk") };
        var cut = Surface(lines);
        Send(cut, new { command = "enter", lines, anchor = Caret(1, 4), focus = Caret(1, 4), at = 0 });

        var undone = Send(cut, new { command = "undo" });

        Assert.Equal(lines, cut.Instance.Lines);
        Assert.Equal(new SurfacePointAnswer(1, 4), FocusOf(undone!));

        var redone = Send(cut, new { command = "redo" });

        Assert.Equal([Text("Shopping"), Box("milk"), Box("")], cut.Instance.Lines);
        Assert.Equal(new SurfacePointAnswer(2, 0), FocusOf(redone!));
    }

    [Fact]
    public void Typing_the_browser_reports_is_undone_a_word_at_a_time()
    {
        var cut = Surface(Text(""));
        var typed = string.Empty;
        foreach (var (character, at) in "ab c".Select((character, index) => (character, index * 100.0)))
        {
            var before = typed.Length;
            typed += character;
            Send(cut, new
            {
                command = "typed", lines = new[] { Text(typed) }, anchor = Caret(0, typed.Length), focus = Caret(0, typed.Length),
                beforeAnchor = Caret(0, before), beforeFocus = Caret(0, before), inputType = "insertText", text = character.ToString(), at
            });
        }

        Send(cut, new { command = "undo" });
        Assert.Equal([Text("ab ")], cut.Instance.Lines);

        var toTheStart = Send(cut, new { command = "undo" });
        Assert.Equal([Text("")], cut.Instance.Lines);
        Assert.Equal(new SurfacePointAnswer(0, 0), FocusOf(toTheStart!));
    }

    [Fact]
    public async Task The_boxes_to_ring_are_the_ones_a_press_would_answer_for_and_the_page_hears_how_many()
    {
        var counts = new List<int>();
        var lines = new[] { Text("Shopping"), Box("milk"), Box("eggs"), Text("then") };
        var cut = RenderComponent<ChecklistTextEditor>(parameters => parameters
            .Add(editor => editor.Lines, lines)
            .Add(editor => editor.SelectedTicksChanged, count => counts.Add(count)));

        var ringed = await cut.InvokeAsync(() => cut.Instance.SelectedChecklistLines(JsonSerializer.Serialize(
            new { command = "select", lines, anchor = Caret(0, 2), focus = Caret(3, 1) }, Json)));
        var justACaret = await cut.InvokeAsync(() => cut.Instance.SelectedChecklistLines(JsonSerializer.Serialize(
            new { command = "select", lines, anchor = Caret(1, 2), focus = Caret(1, 2) }, Json)));
        await cut.InvokeAsync(() => cut.Instance.OnSelectedTicksChanged(2));

        Assert.Equal([1, 2], ringed);
        Assert.Empty(justACaret);
        Assert.Equal([2], counts);
    }

    [Fact]
    public void A_press_on_one_of_several_selected_boxes_ticks_them_all()
    {
        var lines = new[] { Box("milk"), Box("eggs"), Box("bread") };
        var cut = Surface(lines);

        Send(cut, new { command = "tick", line = 0, lines, anchor = Caret(0, 0), focus = Caret(1, 4) });

        Assert.Equal([Box("milk") with { IsChecked = true }, Box("eggs") with { IsChecked = true }, Box("bread")], cut.Instance.Lines);
    }

    [Fact]
    public void Undo_with_nothing_to_undo_answers_nothing()
    {
        var cut = Surface(Text("abc"));

        Assert.Null(Send(cut, new { command = "undo" }));
        Assert.Null(Send(cut, new { command = "redo" }));
    }

    [Fact]
    public void A_press_from_the_toolbar_with_no_caret_on_the_surface_starts_a_box_under_the_last_line()
    {
        var cut = Surface(Text("Shopping"), Text("milk"));

        var answer = Send(cut, new { command = "checklistItem", lines = new[] { Text("Shopping"), Text("milk") } });

        Assert.NotNull(answer);
        Assert.Equal([Text("Shopping"), Text("milk"), Box("")], cut.Instance.Lines);
    }
}
