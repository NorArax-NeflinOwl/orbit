using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Sections;
using Orbit.Web.Components;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// What a page is narrowed by, folded into one button on a phone. There is no viewport here to test
/// against - which of the two shapes is drawn is CSS's answer, not the component's - so what these pin
/// is the part that has to be true at both widths: the controls exist exactly once, whatever the button
/// has been doing, and the button says which state it is in.
///
/// Exactly once is the whole point. The obvious way to write this - the controls twice, one copy for
/// each width, hidden by a media query - draws two sets of folder tabs, and pressing the hidden one
/// still works.
///
/// The button is drawn through a section, in the page header's own row (see PageToolbarTrigger), so
/// every test here renders an outlet beside the toolbar - without one the button has nowhere to land,
/// which is exactly what a page that forgot its PageHeader would see.
/// </summary>
public sealed class PhoneToolbarTests : OrbitTestContext
{
    [Fact]
    public void The_controls_it_is_given_are_in_it_exactly_once()
    {
        var cut = RenderToolbarWithItsHeader();

        Assert.Single(cut.FindAll(".phone-toolbar-panel .chip"));
    }

    /// <summary>
    /// Still exactly once when it is open: opening moves nothing and copies nothing, it only changes
    /// what CSS does with the panel that was there all along.
    /// </summary>
    [Fact]
    public void Opening_it_does_not_make_a_second_copy()
    {
        var cut = RenderToolbarWithItsHeader();

        cut.Find(".phone-toolbar-trigger").Click();

        Assert.Single(cut.FindAll(".phone-toolbar-panel .chip"));
        Assert.Contains("open", cut.Find(".phone-toolbar").ClassList);
        Assert.Equal("true", cut.Find(".phone-toolbar-trigger").GetAttribute("aria-expanded"));
    }

    /// <summary>
    /// Shut by pressing anywhere else, the way every panel that covers a page is. Only from outside:
    /// nothing inside the panel closes it, because these are settings rather than actions and choosing
    /// a folder and then two chips is one visit rather than three. That half shows up here as the
    /// absence of a handler - pressing a chip reaches the page's own, and nothing of this component's.
    /// </summary>
    [Fact]
    public void It_shuts_when_the_page_behind_it_is_pressed()
    {
        var cut = RenderToolbarWithItsHeader();
        cut.Find(".phone-toolbar-trigger").Click();

        cut.Find(".phone-toolbar-backdrop").Click();

        Assert.DoesNotContain("open", cut.Find(".phone-toolbar").ClassList);
        Assert.Empty(cut.FindAll(".phone-toolbar-backdrop"));
    }

    /// <summary>
    /// The button belongs to the page's header row, not to a row of its own under it. On a phone that
    /// row held one button and pushed the first card down a whole screen's worth of the answer.
    /// </summary>
    [Fact]
    public void The_button_is_drawn_in_the_page_header_rather_than_beside_the_panel()
    {
        var cut = Render(builder =>
        {
            builder.OpenComponent<PageHeader>(0);
            builder.AddComponentParameter(
                1,
                nameof(PageHeader.Title),
                (RenderFragment)(title => title.AddContent(0, "Notes")));
            builder.CloseComponent();

            builder.OpenComponent<PhoneToolbar>(2);
            builder.AddComponentParameter(
                3,
                nameof(PhoneToolbar.ChildContent),
                (RenderFragment)(content => content.AddMarkupContent(0, "<button type=\"button\" class=\"chip\">All</button>")));
            builder.CloseComponent();
        });

        Assert.Single(cut.FindAll(".page-header .phone-toolbar-trigger"));
        Assert.Empty(cut.FindAll(".phone-toolbar .phone-toolbar-trigger"));
    }

    /// <summary>
    /// The toolbar and the outlet its button goes to, which on a real page are the page's own header and
    /// the toolbar under it. Rendered together so both halves are in one tree to search.
    /// </summary>
    private IRenderedFragment RenderToolbarWithItsHeader()
        => Render(builder =>
        {
            builder.OpenComponent<SectionOutlet>(0);
            builder.AddComponentParameter(1, nameof(SectionOutlet.SectionName), PageToolbarTrigger.SectionName);
            builder.CloseComponent();

            builder.OpenComponent<PhoneToolbar>(2);
            builder.AddComponentParameter(
                3,
                nameof(PhoneToolbar.ChildContent),
                (RenderFragment)(content => content.AddMarkupContent(0, "<button type=\"button\" class=\"chip\">All</button>")));
            builder.CloseComponent();
        });
}
