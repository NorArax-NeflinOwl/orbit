using Bunit;
using Microsoft.AspNetCore.Components;
using Orbit.Web.Components;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// The title-and-description pair, which both the task list and the warehouse editors put at the top of
/// their form. What is pinned here is that both boxes show what they were given and report what is typed
/// into them - a box that shows a value but never reports a change is a field that silently does not save.
/// </summary>
public sealed class TitledDescriptionTests : OrbitTestContext
{
    [Fact]
    public void Both_boxes_show_what_they_were_given()
    {
        var cut = Render("Pantry", "Everything that lives in the cellar");

        Assert.Equal("Pantry", cut.Find(".titled-description-title").GetAttribute("value"));
        Assert.Equal("Everything that lives in the cellar", cut.Find(".titled-description-body").GetAttribute("value"));
    }

    [Fact]
    public void Typing_a_title_reports_it_as_it_is_typed()
    {
        // As it is typed rather than on leaving the box: the name suggestions underneath follow this.
        var typed = new List<string>();
        var cut = Render("", "", onTitle: value => typed.Add(value));

        cut.Find(".titled-description-title").Input("Pant");

        Assert.Equal(["Pant"], typed);
    }

    [Fact]
    public void Typing_a_description_reports_it_too()
    {
        var typed = new List<string>();
        var cut = Render("Pantry", "", onDescription: value => typed.Add(value));

        cut.Find(".titled-description-body").Input("The cellar, and the shelf by the door");

        Assert.Equal(["The cellar, and the shelf by the door"], typed);
    }

    /// <summary>
    /// The title is drawn as a heading, so it carries no visible label - which leaves a screen reader
    /// with an unnamed box unless the name is given to it another way.
    /// </summary>
    [Fact]
    public void The_title_box_is_named_even_though_nothing_is_written_beside_it()
    {
        var cut = Render("Pantry", "");

        Assert.Equal("Name", cut.Find(".titled-description-title").GetAttribute("aria-label"));
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
