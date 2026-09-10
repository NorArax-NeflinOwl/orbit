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
    public void It_carries_the_actions_and_whatever_the_page_puts_beside_them()
    {
        var saved = false;
        var cancelled = false;
        var cut = RenderComponent<EditorRail>(parameters => parameters
            .Add(rail => rail.OnSave, () => saved = true)
            .Add(rail => rail.OnCancel, () => cancelled = true)
            .AddChildContent("<button type=\"button\" class=\"beside\">Delete</button>"));

        cut.FindAll(".editor-rail-actions button").First(button => button.GetAttribute("aria-label") == "Save").Click();
        cut.FindAll(".editor-rail-actions button").First(button => button.GetAttribute("aria-label") == "Back").Click();

        Assert.True(saved);
        Assert.True(cancelled);
        // Beside the rest rather than anywhere else on the page: it is the same panel and the same edge.
        Assert.Single(cut.FindAll(".editor-rail-actions .beside"));
    }

    /// <summary>
    /// Edit sits between Save and the way out, on the screens that read an object. It used to be a line
    /// in the overflow menu on all five of them - one press to open the menu and another to find it, for
    /// the single thing somebody is most likely to want after reading.
    /// </summary>
    [Fact]
    public void Edit_sits_between_saving_and_leaving()
    {
        var cut = RenderComponent<EditorRail>(parameters => parameters
            .Add(rail => rail.OnSave, () => { })
            .Add(rail => rail.OnEdit, () => { })
            .Add(rail => rail.OnCancel, () => { }));

        Assert.Equal(
            ["Save", "Edit", "Back"],
            cut.FindAll(".editor-rail-actions button").Select(button => button.GetAttribute("aria-label")));
    }

    /// <summary>
    /// And a screen with no form behind it carries no Edit at all, the same way one with nothing to
    /// write carries no Save - a button that does nothing is worse than no button.
    /// </summary>
    [Fact]
    public void A_screen_with_no_form_behind_it_carries_no_edit()
    {
        var cut = RenderComponent<EditorRail>(parameters => parameters.Add(rail => rail.OnCancel, () => { }));

        Assert.DoesNotContain(
            cut.FindAll(".editor-rail-actions button"),
            button => button.GetAttribute("aria-label") == "Edit");
    }

    /// <summary>
    /// What that button says is the page's to decide: a read-only share may open the form to look at it
    /// and not to write in it, and calling that "Edit" promises something it cannot keep.
    /// </summary>
    [Fact]
    public void A_reader_who_may_not_write_is_offered_View()
    {
        var cut = RenderComponent<EditorRail>(parameters => parameters
            .Add(rail => rail.OnEdit, () => { })
            .Add(rail => rail.EditLabel, "View")
            .Add(rail => rail.OnCancel, () => { }));

        Assert.Single(cut.FindAll("button[aria-label=View]"));
    }

    /// <summary>
    /// Nothing to save *yet* is said by the button rather than by leaving it out: the page can write,
    /// and there is nothing written to write. See .page-action.
    /// </summary>
    [Fact]
    public void Nothing_to_save_yet_greys_the_save()
    {
        var cut = RenderComponent<EditorRail>(parameters => parameters
            .Add(rail => rail.OnSave, () => { })
            .Add(rail => rail.SaveDisabled, true));

        Assert.True(cut.Find(".page-action-primary").HasAttribute("disabled"));
    }

    /// <summary>
    /// A screen that is read rather than filled in hands the panel no Save, and the button is not drawn
    /// at all - an appointment has nothing that can be changed where it stands, and a Save that does
    /// nothing is worse than no Save. What leaves the screen stays where it always is.
    /// </summary>
    [Fact]
    public void A_screen_with_nothing_to_write_carries_no_save()
    {
        var cut = RenderComponent<EditorRail>(parameters => parameters.Add(rail => rail.OnCancel, () => { }));

        Assert.Empty(cut.FindAll(".page-action-primary"));
        Assert.Single(cut.FindAll("button[aria-label=Back]"));
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

    /// <summary>
    /// The two things every editor keeps here are asked for as text rather than as a fragment, and this
    /// is why: a fragment holding two conditionals is a fragment whether or not either of them is true,
    /// so every editing screen carried an arrow that opened onto nothing until something went wrong.
    /// </summary>
    [Fact]
    public void A_screen_where_nothing_is_wrong_has_no_arrow()
    {
        var cut = RenderComponent<EditorRail>(parameters => parameters
            .Add(rail => rail.Notice, null)
            .Add(rail => rail.ErrorMessage, null));

        Assert.Empty(cut.FindAll(".editor-rail-toggle"));
    }

    [Fact]
    public void Somebody_else_holding_the_screen_is_said_up_here()
    {
        var cut = RenderComponent<EditorRail>(parameters => parameters
            .Add(rail => rail.Notice, "Ada is currently editing this note - you can't edit it right now."));

        Assert.Single(cut.FindAll(".editor-rail-toggle"));
        Assert.Contains("Ada is currently editing", cut.Find(".editor-rail-extras .lock-banner").TextContent);
    }

    /// <summary>
    /// Beside the button that failed rather than at the end of a form taller than the screen: a save
    /// that did not happen is answered where Save is, and an answer nobody scrolls to is not one.
    /// </summary>
    [Fact]
    public void A_save_that_did_not_happen_is_said_beside_Save()
    {
        var cut = RenderComponent<EditorRail>(parameters => parameters
            .Add(rail => rail.OnSave, EventCallback.Factory.Create(this, () => { }))
            .Add(rail => rail.ErrorMessage, "That could not be saved."));

        Assert.Single(cut.FindAll(".editor-rail-toggle"));
        Assert.Equal("That could not be saved.", cut.Find(".editor-rail-extras .error").TextContent);
    }
}
