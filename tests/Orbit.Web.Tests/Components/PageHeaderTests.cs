using Bunit;
using Microsoft.AspNetCore.Components;
using Orbit.Web.Components;
using Xunit;
using static Orbit.Web.Markup;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// The block every screen opens with. What matters here is what a page can leave out: no leading
/// button, no subtitle, no actions - each is drawn only when a page hands it one, rather than as an
/// empty row or an empty paragraph still taking up the line it would have read on.
/// </summary>
public sealed class PageHeaderTests : OrbitTestContext
{
    [Fact]
    public void It_carries_the_title()
    {
        var cut = RenderComponent<PageHeader>(parameters => parameters
            .Add(header => header.Title, (RenderFragment)(builder => builder.AddContent(0, "Notes"))));

        Assert.Equal("Notes", cut.Find("h1").TextContent);
    }

    /// <summary>A page with nothing to add makes an empty row a control nobody presses - see .page-add
    /// on the four screens that do carry one.</summary>
    [Fact]
    public void With_no_leading_action_the_heading_is_not_a_row()
    {
        var cut = RenderComponent<PageHeader>(parameters => parameters
            .Add(header => header.Title, (RenderFragment)(builder => builder.AddContent(0, "Notes"))));

        Assert.Null(cut.Find(".page-header > div").GetAttribute("class"));
    }

    [Fact]
    public void A_leading_action_turns_the_heading_into_a_row()
    {
        var cut = RenderComponent<PageHeader>(parameters => parameters
            .Add(header => header.Title, (RenderFragment)(builder => builder.AddContent(0, "Notes")))
            .Add(header => header.LeadingAction, (RenderFragment)(builder => builder.AddMarkupContent(0, "<button>Add</button>"))));

        Assert.Equal("page-heading", cut.Find(".page-header > div").GetAttribute("class"));
        Assert.NotEmpty(cut.FindAll(".page-heading button"));
    }

    /// <summary>Nothing said under the title is nothing drawn, not an empty paragraph still carrying
    /// its own margin - see Markup.Optional, which is what a page reaches for to say so.</summary>
    [Fact]
    public void With_no_subtitle_nothing_is_drawn_under_the_title()
    {
        var cut = RenderComponent<PageHeader>(parameters => parameters
            .Add(header => header.Title, (RenderFragment)(builder => builder.AddContent(0, "Note")))
            .Add(header => header.Subtitle, Optional(null)));

        Assert.Empty(cut.FindAll(".page-subtitle"));
    }

    [Fact]
    public void A_subtitle_reads_under_the_title()
    {
        var cut = RenderComponent<PageHeader>(parameters => parameters
            .Add(header => header.Title, (RenderFragment)(builder => builder.AddContent(0, "Note")))
            .Add(header => header.Subtitle, Optional("Shared by Anna")));

        Assert.Equal("Shared by Anna", cut.Find(".page-subtitle").TextContent);
    }

    [Fact]
    public void A_hint_reads_beside_the_title_rather_than_as_a_subtitle()
    {
        var cut = RenderComponent<PageHeader>(parameters => parameters
            .Add(header => header.Title, (RenderFragment)(builder => builder.AddContent(0, "New note")))
            .Add(header => header.Hint, Optional("Write something in it.")));

        Assert.Equal("Write something in it.", cut.Find(".form-hint").TextContent);
        Assert.Empty(cut.FindAll(".page-subtitle"));
    }

    /// <summary>A page with nothing at the header's other end draws no empty row there either - see
    /// GroupMembers.razor, the one screen with no control of its own.</summary>
    [Fact]
    public void With_no_actions_nothing_sits_at_the_headers_other_end()
    {
        var cut = RenderComponent<PageHeader>(parameters => parameters
            .Add(header => header.Title, (RenderFragment)(builder => builder.AddContent(0, "Members"))));

        Assert.Empty(cut.FindAll(".page-header-actions"));
    }

    [Fact]
    public void Actions_sit_at_the_headers_other_end()
    {
        var cut = RenderComponent<PageHeader>(parameters => parameters
            .Add(header => header.Title, (RenderFragment)(builder => builder.AddContent(0, "Notifications")))
            .Add(header => header.Actions, (RenderFragment)(builder => builder.AddMarkupContent(0, "<button>Clear</button>"))));

        Assert.Contains("Clear", cut.Find(".page-header-actions").TextContent);
    }
}
