using Bunit;
using Microsoft.AspNetCore.Components;
using Orbit.Web.Components;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// The editing screens' second panel. What matters here is that the two actions are always in it and
/// always reachable, and that anything a page keeps beside them can be folded away - on a phone the
/// panel is a bar along the foot of the window, and a bar tall enough to hold fields as well would be
/// most of the screen.
/// </summary>
public sealed class EditorRailTests : OrbitTestContext
{
    [Fact]
    public void It_carries_the_two_actions_and_whatever_the_page_puts_beside_them()
    {
        var saved = false;
        var cancelled = false;
        var cut = RenderComponent<EditorRail>(parameters => parameters
            .Add(rail => rail.OnSave, () => saved = true)
            .Add(rail => rail.OnCancel, () => cancelled = true)
            .AddChildContent("<button type=\"button\" class=\"beside\">Delete</button>"));

        cut.FindAll(".editor-rail-actions button").First(button => button.GetAttribute("aria-label") == "Save").Click();
        cut.FindAll(".editor-rail-actions button").First(button => button.GetAttribute("aria-label") == "Cancel").Click();

        Assert.True(saved);
        Assert.True(cancelled);
        // Beside the two rather than anywhere else on the page: it is the same panel and the same edge.
        Assert.Single(cut.FindAll(".editor-rail-actions .beside"));
    }

    /// <summary>Nothing to save is said by the button rather than by leaving it out - see .page-action.</summary>
    [Fact]
    public void Nothing_to_save_greys_the_save()
    {
        var cut = RenderComponent<EditorRail>(parameters => parameters.Add(rail => rail.SaveDisabled, true));

        Assert.True(cut.Find(".page-action-primary").HasAttribute("disabled"));
    }

    /// <summary>
    /// A page with nothing to keep in view has nothing to unfold, so the arrow is not drawn at all -
    /// a control that opens an empty panel is a control that does nothing.
    /// </summary>
    [Fact]
    public void With_nothing_to_keep_in_view_there_is_no_arrow()
    {
        var cut = RenderComponent<EditorRail>();

        Assert.Empty(cut.FindAll(".editor-rail-toggle"));
        Assert.Empty(cut.FindAll(".editor-rail-extras"));
    }

    [Fact]
    public void What_the_page_keeps_in_view_folds_away_behind_the_arrow()
    {
        var cut = RenderComponent<EditorRail>(parameters => parameters
            .Add(rail => rail.Extras, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>Two of six done</p>"))));

        var arrow = cut.Find(".editor-rail-toggle");
        Assert.Equal("false", arrow.GetAttribute("aria-expanded"));
        Assert.Equal("Expand", arrow.GetAttribute("aria-label"));
        // Written whether or not it is folded: what folds it away on a narrow screen is the stylesheet,
        // so a wide one shows it without anybody pressing anything.
        Assert.Contains("Two of six done", cut.Find(".editor-rail-extras").TextContent);

        arrow.Click();

        Assert.Contains("editor-rail-open", cut.Find(".editor-rail").ClassName);
        Assert.Equal("true", cut.Find(".editor-rail-toggle").GetAttribute("aria-expanded"));
    }
}
