using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// When pressing a position somebody shared should leave Orbit's own map for the device's - see
/// MapAppHandoff. The button beside the row is always there; this is about the row itself.
/// </summary>
public sealed class MapAppHandoffTests
{
    /// <summary>
    /// The case this exists for: a phone whose reader keeps third parties out has no map background at
    /// all (see mapTiles.js), so centring a blank square on a pin answers nothing.
    /// </summary>
    [Fact]
    public void A_phone_with_no_map_to_draw_hands_the_pin_over()
        => Assert.True(MapAppHandoff.ShouldOpenTheMapApp(
            isPhone: true, mapTilesAllowed: false, knowsWhereTheReaderIs: true));

    /// <summary>
    /// And so does one Orbit has never been told the position of: it can draw where somebody else is and
    /// nothing about where that is from here, which is the question a shared pin asks.
    /// </summary>
    [Fact]
    public void A_phone_that_does_not_know_where_its_reader_is_hands_the_pin_over()
        => Assert.True(MapAppHandoff.ShouldOpenTheMapApp(
            isPhone: true, mapTilesAllowed: true, knowsWhereTheReaderIs: false));

    /// <summary>A phone whose map can answer keeps the ordinary gesture: the pin is centred, on the page.</summary>
    [Fact]
    public void A_phone_whose_map_can_answer_stays_on_the_page()
        => Assert.False(MapAppHandoff.ShouldOpenTheMapApp(
            isPhone: true, mapTilesAllowed: true, knowsWhereTheReaderIs: true));

    /// <summary>
    /// Never on a desktop, whatever else is true: there is usually no map app to open, the map is big
    /// enough to read, and a press that navigated away from the page would be a surprise.
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void A_desktop_never_leaves_the_page(bool mapTilesAllowed, bool knowsWhereTheReaderIs)
        => Assert.False(MapAppHandoff.ShouldOpenTheMapApp(isPhone: false, mapTilesAllowed, knowsWhereTheReaderIs));
}
