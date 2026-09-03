using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Orbit.Contracts.Notes;
using Orbit.Web.Components;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// The title-and-description field, which both the task list and the inventory editors put at the top of
/// their form. One surface rather than two boxes: the first line is the title and everything under it is
/// the description - the note editor's own shape, reused.
///
/// The surface itself is contenteditable driven from JS, which a test renderer has none of, so what is
/// pinned here is the mapping either side of it: which lines it is handed, and what comes back out of the
/// two strings the pages store. A field that shows a value but never reports a change is a field that
/// silently does not save, and that is the failure these guard.
/// </summary>
public sealed class TitledDescriptionTests : OrbitTestContext
{
    [Fact]
    public void The_first_line_is_the_title_and_the_rest_is_the_description()
    {
        var cut = Render("Pantry", "Everything that lives in the cellar");

        Assert.Equal(
            ["Pantry", "Everything that lives in the cellar"],
            LinesHandedToTheEditor(cut));
    }

    /// <summary>
    /// A description of several lines is several lines, not one with newlines in it: the surface knows
    /// nothing about "\n" - each of its lines is an element of its own.
    /// </summary>
    [Fact]
    public void A_description_of_several_lines_is_handed_over_as_several()
    {
        var cut = Render("Pantry", "The cellar\nand the shelf by the door");

        Assert.Equal(["Pantry", "The cellar", "and the shelf by the door"], LinesHandedToTheEditor(cut));
    }

    /// <summary>
    /// Nothing written under the title is no line at all. One empty line would open the field with a
    /// blank row under the heading and the caret free to land in it.
    /// </summary>
    [Fact]
    public void A_field_with_no_description_is_handed_the_title_alone()
    {
        var cut = Render("Pantry", string.Empty);

        Assert.Equal(["Pantry"], LinesHandedToTheEditor(cut));
    }

    [Fact]
    public void What_is_written_on_the_first_line_comes_back_as_the_title()
    {
        var typed = new List<string>();
        var cut = Render(string.Empty, string.Empty, onTitle: value => typed.Add(value));

        WriteIntoTheEditor(cut, "Pant");

        Assert.Equal(["Pant"], typed);
    }

    [Fact]
    public void What_is_written_under_it_comes_back_as_the_description()
    {
        var typed = new List<string>();
        var cut = Render("Pantry", string.Empty, onDescription: value => typed.Add(value));

        WriteIntoTheEditor(cut, "Pantry", "The cellar, and the shelf by the door");

        Assert.Equal(["The cellar, and the shelf by the door"], typed);
    }

    /// <summary>Every line under the first is the description, joined back into the one string the pages store.</summary>
    [Fact]
    public void Several_lines_come_back_as_one_description()
    {
        var typed = new List<string>();
        var cut = Render("Pantry", string.Empty, onDescription: value => typed.Add(value));

        WriteIntoTheEditor(cut, "Pantry", "The cellar", "and the shelf by the door");

        Assert.Equal(["The cellar\nand the shelf by the door"], typed);
    }

    /// <summary>
    /// Only what changed is reported. Editing the description must not also announce a title nobody
    /// touched - the name suggestions follow the title, and would restart on every keystroke below it.
    /// </summary>
    [Fact]
    public void A_change_under_the_title_says_nothing_about_the_title()
    {
        var titles = new List<string>();
        var cut = Render("Pantry", string.Empty, onTitle: value => titles.Add(value));

        WriteIntoTheEditor(cut, "Pantry", "The cellar");

        Assert.Empty(titles);
    }

    /// <summary>
    /// An emptied field still reports both. A title nobody has typed yet is an empty title, not a
    /// missing one - and a form that never hears about it keeps saving the old name.
    /// </summary>
    [Fact]
    public void Clearing_the_field_reports_both_as_empty()
    {
        var titles = new List<string>();
        var descriptions = new List<string>();
        var cut = Render("Pantry", "The cellar", onTitle: titles.Add, onDescription: descriptions.Add);

        WriteIntoTheEditor(cut, string.Empty);

        Assert.Equal([string.Empty], titles);
        Assert.Equal([string.Empty], descriptions);
    }

    /// <summary>
    /// The field is drawn as a heading, so it carries no visible label - which leaves a screen reader
    /// with an unnamed surface unless the name is given to it another way.
    /// </summary>
    [Fact]
    public void The_field_is_named_even_though_nothing_is_written_beside_it()
    {
        var cut = Render("Pantry", string.Empty);

        Assert.Equal("Name", cut.Find(".titled-description-editor").GetAttribute("aria-label"));
    }

    /// <summary>What the editing surface was handed to draw itself from.</summary>
    private static string[] LinesHandedToTheEditor(IRenderedComponent<TitledDescription> cut)
        => [.. cut.FindComponent<ChecklistTextEditor>().Instance.Lines.Select(line => line.Text)];

    /// <summary>
    /// What the surface reports after somebody has typed into it. Called the way its own JavaScript
    /// calls it, so the mapping under test is the one that actually runs.
    /// </summary>
    private static void WriteIntoTheEditor(IRenderedComponent<TitledDescription> cut, params string[] lines)
    {
        var editor = cut.FindComponent<ChecklistTextEditor>().Instance;
        var written = lines.Select(line => new NoteContentLineDto(line, IsChecklistItem: false, IsChecked: false));
        cut.InvokeAsync(() => editor.OnLinesChangedFromJs(
            JsonSerializer.Serialize(written, new JsonSerializerOptions(JsonSerializerDefaults.Web))))
            .GetAwaiter().GetResult();
    }

    private IRenderedComponent<TitledDescription> Render(
        string title, string description, Action<string>? onTitle = null, Action<string>? onDescription = null)
        => RenderComponent<TitledDescription>(parameters => parameters
            .Add(control => control.Title, title)
            .Add(control => control.Description, description)
            .Add(control => control.TitleLabel, "Name")
            .Add(control => control.TitleChanged, EventCallback.Factory.Create<string>(this, value => onTitle?.Invoke(value)))
            .Add(control => control.DescriptionChanged, EventCallback.Factory.Create<string>(this, value => onDescription?.Invoke(value))));
}
