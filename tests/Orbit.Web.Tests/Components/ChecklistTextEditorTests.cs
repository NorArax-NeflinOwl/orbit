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
    public void A_press_from_the_toolbar_with_no_caret_on_the_surface_starts_a_box_under_the_last_line()
    {
        var cut = Surface(Text("Shopping"), Text("milk"));

        var answer = Send(cut, new { command = "checklistItem", lines = new[] { Text("Shopping"), Text("milk") } });

        Assert.NotNull(answer);
        Assert.Equal([Text("Shopping"), Text("milk"), Box("")], cut.Instance.Lines);
    }
}
