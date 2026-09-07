using Orbit.Core.Advertising;
using Xunit;

namespace Orbit.Api.Tests.Advertising;

/// <summary>
/// What the advertising slots show. Orbit's own things, served by Orbit - see HouseAds for why no third
/// party is involved and what putting one behind these slots would mean.
/// </summary>
public sealed class HouseAdsTests
{
    /// <summary>
    /// The one rule that keeps these safe to show without asking anybody's permission first: an advert
    /// goes where Orbit itself goes. An absolute address here would be a request to somebody else's
    /// server, made on the reader's behalf, which is exactly what the consent question is about.
    /// </summary>
    [Fact]
    public void Every_advert_leads_somewhere_on_this_Orbit()
        => Assert.All(HouseAds.All, advert => Assert.StartsWith("/", advert.Url));

    [Fact]
    public void Every_advert_has_something_to_say_and_a_way_in()
        => Assert.All(HouseAds.All, advert =>
        {
            Assert.NotEmpty(advert.Title);
            Assert.NotEmpty(advert.Body);
            Assert.NotEmpty(advert.ActionLabel);
        });

    /// <summary>
    /// "Get Orbit on your phone", read on a phone that already has it, is the one advert that makes its
    /// reader trust the rest of them less - see HouseAd.ShowsOnAPhone.
    /// </summary>
    [Fact]
    public void The_app_does_not_advertise_itself_to_somebody_already_using_it()
        => Assert.All(HouseAds.OnAPhone, advert => Assert.True(advert.ShowsOnAPhone));

    /// <summary>
    /// Any number is an answer, and the same number is always the same advert: a slot that re-picked
    /// would change under the reader's eye every time anything on the page was drawn again.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void A_slot_number_always_names_one_advert(int slot)
    {
        Assert.NotNull(HouseAds.ForSlot(slot));
        Assert.Equal(HouseAds.ForSlot(slot), HouseAds.ForSlot(slot));
        Assert.NotNull(HouseAds.ForSlotOnAPhone(slot));
    }
}
