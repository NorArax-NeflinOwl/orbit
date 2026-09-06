using Bunit;
using Orbit.Web.Components;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// Drawing a description with the addresses in it pressable. What the splitter decides is covered by
/// LinksInTextTests; what matters here is that none of it ever reaches the page as markup.
/// </summary>
public sealed class TextWithLinksTests : TestContext
{
    private IRenderedComponent<TextWithLinks> Render(string? text)
        => RenderComponent<TextWithLinks>(parameters => parameters.Add(component => component.Text, text));

    [Fact]
    public void An_address_becomes_a_link_to_itself()
    {
        var cut = Render("See https://example.com/menu before Friday.");

        var link = cut.Find("a");
        Assert.Equal("https://example.com/menu", link.GetAttribute("href"));
        Assert.Equal("https://example.com/menu", link.TextContent);
        // The words around it survive, in place.
        Assert.Contains("See", cut.Markup);
        Assert.Contains("before Friday.", cut.Markup);
    }

    /// <summary>
    /// A new tab, because a description is read in the middle of doing something - and noopener with
    /// it, so the page opened cannot reach back through window.opener.
    /// </summary>
    [Fact]
    public void A_link_opens_in_a_new_tab_and_cannot_reach_back()
    {
        var link = Render("https://example.com").Find("a");

        Assert.Equal("_blank", link.GetAttribute("target"));
        Assert.Equal("noopener noreferrer", link.GetAttribute("rel"));
    }

    /// <summary>
    /// The property the whole design rests on. A description can be written by whoever shared the thing
    /// it sits on, so what is drawn is content and never markup - the tags come out as the characters
    /// somebody typed, and there is no element in the output but the link.
    /// </summary>
    [Fact]
    public void Markup_written_into_a_description_is_shown_rather_than_run()
    {
        var cut = Render("<script>alert('x')</script> and <b>bold</b> at https://example.com");

        Assert.Empty(cut.FindAll("script"));
        Assert.Empty(cut.FindAll("b"));
        Assert.Contains("&lt;script&gt;", cut.Markup);
        // And the ordinary case still works beside it.
        Assert.Equal("https://example.com", cut.Find("a").GetAttribute("href"));
    }

    /// <summary>An href is only ever http or https - see LinksInText for why that is a list of what is allowed.</summary>
    [Fact]
    public void A_dangerous_scheme_is_drawn_as_words_and_never_as_an_href()
    {
        var cut = Render("javascript:alert(1)");

        Assert.Empty(cut.FindAll("a"));
        Assert.Contains("javascript:alert(1)", cut.Markup);
    }

    [Fact]
    public void Nothing_written_draws_nothing_at_all()
        => Assert.Equal(string.Empty, Render(null).Markup.Trim());
}
