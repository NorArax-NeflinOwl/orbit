using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Core.Advertising;
using Orbit.Web.Components;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// The advertising slots beside the page. Both are drawn from one component and told apart by the
/// window's width in CSS, so what is asserted here is that both are there and say what they should -
/// which of them a given window shows is .ad-rail and .ad-banner's own business.
/// </summary>
public sealed class AdSlotTests : OrbitTestContext
{
    [Fact]
    public void A_slot_draws_the_rail_and_the_bar_from_one_advert()
    {
        var cut = RenderComponent<AdSlot>(parameters => parameters.Add(slot => slot.Slot, 0));

        Assert.Single(cut.FindAll(".ad-rail"));
        Assert.Single(cut.FindAll(".ad-banner"));
        var advert = HouseAds.All[0];
        Assert.All(
            cut.FindAll(".ad-title"),
            title => Assert.Equal(advert.Title, title.TextContent.Trim()));
    }

    /// <summary>
    /// Said out loud on every slot: a reader is entitled to know which part of a page is an advert, and
    /// a house advert that read as Orbit talking would be the one kind worth objecting to.
    /// </summary>
    [Fact]
    public void Every_slot_says_it_is_one()
    {
        var cut = RenderComponent<AdSlot>(parameters => parameters.Add(slot => slot.Slot, 0));

        Assert.Equal(2, cut.FindAll(".ad-mark").Count);
        Assert.All(cut.FindAll(".ad-mark"), mark => Assert.Equal("Ad", mark.TextContent.Trim()));
    }

    /// <summary>
    /// It goes where Orbit itself goes. Nothing here is fetched from anywhere else, which is what makes
    /// these slots cost no third-party request and tell nobody that this reader was here.
    /// </summary>
    [Fact]
    public void An_advert_leads_somewhere_on_this_Orbit()
    {
        var cut = RenderComponent<AdSlot>(parameters => parameters.Add(slot => slot.Slot, 0));

        Assert.All(
            cut.FindAll(".ad-action"),
            action => Assert.StartsWith("/", action.GetAttribute("href")));
    }

    /// <summary>
    /// The slot number picks, and wraps - so any number is an answer and the same number is the same
    /// advert. A slot that re-picked at random would change under the reader's eye on every render.
    /// </summary>
    [Fact]
    public void The_same_slot_number_always_shows_the_same_advert()
    {
        var first = RenderComponent<AdSlot>(parameters => parameters.Add(slot => slot.Slot, 7));
        var again = RenderComponent<AdSlot>(parameters => parameters.Add(slot => slot.Slot, 7));

        Assert.Equal(
            first.Find(".ad-rail .ad-title").TextContent,
            again.Find(".ad-rail .ad-title").TextContent);
    }
}
