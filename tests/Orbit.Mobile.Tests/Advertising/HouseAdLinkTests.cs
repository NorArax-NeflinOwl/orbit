using Orbit.Core.Advertising;
using Orbit.Mobile.Advertising;
using Xunit;

namespace Orbit.Mobile.Tests.Advertising;

/// <summary>
/// Where pressing the advertising bar goes. The bar was not something to tap at all until 2026-09-10,
/// so most of what matters here is the cases where it still is not: there has to be a web client to
/// send anybody to, and the advert has to be Orbit's own.
/// </summary>
public sealed class HouseAdLinkTests
{
    [Fact]
    public void An_advert_opens_its_page_on_this_deployments_web_client()
        => Assert.Equal(
            "https://orbit.example/docs",
            HouseAdLink.For(Advert("/docs"), "https://orbit.example/"));

    /// <summary>
    /// Empty is what the client answers both for a deployment that has not said where its web client is
    /// and for a phone that could not reach the server to ask - see PublicShareClient.WebAddressAsync.
    /// Neither is a reason to open a browser at a guess.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("orbit.example")]
    [InlineData("javascript:alert(1)")]
    public void There_is_nowhere_to_go_without_a_web_address(string webAddress)
        => Assert.Null(HouseAdLink.For(Advert("/docs"), webAddress));

    [Fact]
    public void There_is_nowhere_to_go_without_an_advert()
        => Assert.Null(HouseAdLink.For(null, "https://orbit.example/"));

    /// <summary>
    /// An advert names a path on this Orbit and nothing else - the rule that makes these safe to show
    /// without asking anybody first (see HouseAd.Url). Kept here too, so the day one stops being Orbit's
    /// own copy is the day the press is refused rather than the day it opens somebody else's site.
    /// </summary>
    [Theory]
    [InlineData("https://example.com/deal")]
    [InlineData("//example.com/deal")]
    [InlineData("docs")]
    public void An_advert_pointing_anywhere_but_this_orbit_is_not_opened(string url)
        => Assert.Null(HouseAdLink.For(Advert(url), "https://orbit.example/"));

    /// <summary>Every advert the app actually shows has somewhere to go.</summary>
    [Fact]
    public void Everything_the_app_shows_can_be_opened()
        => Assert.All(
            HouseAds.OnAPhone,
            advert => Assert.NotNull(HouseAdLink.For(advert, "https://orbit.example")));

    private static HouseAd Advert(string url) => new("Title", "Body", "Read it", url);
}
