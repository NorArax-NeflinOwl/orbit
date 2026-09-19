using Bunit;
using Orbit.Web.Components;
using Orbit.Web.Tests;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// Drawing a description with the addresses in it pressable. What the splitter decides is covered by
/// LinksInTextTests; what matters here is that none of it ever reaches the page as markup.
/// </summary>
public sealed class TextWithLinksTests : OrbitTestContext
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

    /// <summary>
    /// A link to somebody else's map opens the place on Orbit's own instead - asked for on 2026-09-19.
    /// In this tab, because it is a page of Orbit's: everything else here leaves for a new one.
    /// </summary>
    [Fact]
    public void A_link_to_somebody_elses_map_opens_orbits_own()
    {
        var cut = Render("Meet me at https://www.google.com/maps/search/?api=1&query=52.2297,21.0122 at six.");

        var link = cut.Find("a");
        Assert.StartsWith("/map?at=52.2297%2C21.0122", link.GetAttribute("href"), StringComparison.Ordinal);
        Assert.Null(link.GetAttribute("target"));
        // The original is still carried, which is what the pin's "Open the original link" opens.
        Assert.Contains("from=https%3A%2F%2Fwww.google.com", link.GetAttribute("href"), StringComparison.Ordinal);
        // And the words are still the words somebody wrote, not Orbit's address for them.
        Assert.Equal("https://www.google.com/maps/search/?api=1&query=52.2297,21.0122", link.TextContent);
    }

    /// <summary>A link that is not a map at all is left exactly as it was written.</summary>
    [Fact]
    public void A_link_that_is_not_a_map_still_goes_where_it_says()
    {
        var link = Render("https://example.com/where-we-are").Find("a");

        Assert.Equal("https://example.com/where-we-are", link.GetAttribute("href"));
        Assert.Equal("_blank", link.GetAttribute("target"));
    }

    /// <summary>
    /// A shortened map link goes to Orbit's map carrying nothing but itself: only the service that made
    /// it knows where it points, and the map page asks the server to follow it rather than this
    /// guessing here - see MapPageLink.
    /// </summary>
    [Fact]
    public void A_shortened_map_link_goes_to_the_map_to_be_followed_there()
    {
        var link = Render("https://maps.app.goo.gl/AbCdEf123").Find("a");

        Assert.Equal("/map?from=https%3A%2F%2Fmaps.app.goo.gl%2FAbCdEf123", link.GetAttribute("href"));
        Assert.Null(link.GetAttribute("target"));
    }

    [Fact]
    public void Nothing_written_draws_nothing_at_all()
        => Assert.Equal(string.Empty, Render(null).Markup.Trim());
}
