using Orbit.Mobile.Screens.Dashboard;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// How far ahead the dashboard's Upcoming card looks on this phone - see UpcomingHorizon and the account
/// screen's Preferences tab. The same five choices Orbit.Web offers, and the same week by default.
/// </summary>
public sealed class UpcomingHorizonTests
{
    private sealed class InMemoryUpcomingHorizonStore : IUpcomingHorizonStore
    {
        private int? _days;

        public int? ReadDays() => _days;

        public void WriteDays(int days) => _days = days;
    }

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-27T09:00:00Z");

    /// <summary>Warsaw, which is two hours ahead in August - the zone the reader this app is for is in.</summary>
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("Two ahead", TimeSpan.FromHours(2), "Two ahead", "Two ahead");

    [Fact]
    public void A_phone_never_asked_looks_a_week_ahead()
    {
        var horizon = new UpcomingHorizon(new InMemoryUpcomingHorizonStore());

        Assert.Equal(UpcomingHorizon.DefaultDays, horizon.Days);
    }

    [Fact]
    public void A_horizon_this_build_does_not_offer_reads_as_the_default()
    {
        var store = new InMemoryUpcomingHorizonStore();
        store.WriteDays(45);
        var horizon = new UpcomingHorizon(store);

        // Written by a version that offered it, or by nobody - either way it is not a horizon to draw a
        // card with, and the same rule DevicePreferences follows in the browser.
        Assert.Equal(UpcomingHorizon.DefaultDays, horizon.Days);
    }

    [Fact]
    public void A_horizon_that_is_not_offered_is_not_stored_either()
    {
        var store = new InMemoryUpcomingHorizonStore();
        var horizon = new UpcomingHorizon(store);

        horizon.SetDays(45);

        Assert.Equal(UpcomingHorizon.DefaultDays, store.ReadDays());
    }

    [Fact]
    public void What_is_inside_the_horizon_is_held_and_what_is_beyond_it_is_not()
    {
        var horizon = new UpcomingHorizon(new InMemoryUpcomingHorizonStore());

        Assert.True(horizon.Holds(Now.AddDays(6), Now, Zone));
        Assert.False(horizon.Holds(Now.AddDays(8), Now, Zone));
    }

    [Fact]
    public void Something_earlier_today_is_still_inside_the_shortest_horizon()
    {
        var store = new InMemoryUpcomingHorizonStore();
        var horizon = new UpcomingHorizon(store);
        horizon.SetDays(1);

        // Measured from the start of today rather than from this moment: a horizon that quietly dropped
        // what is happening this morning would be worse than no horizon at all.
        Assert.True(horizon.Holds(Now.AddHours(-8), Now, Zone));
    }

    [Fact]
    public void No_horizon_holds_everything()
    {
        var horizon = new UpcomingHorizon(new InMemoryUpcomingHorizonStore());
        horizon.SetDays(0);

        Assert.True(horizon.Holds(Now.AddYears(5), Now, Zone));
    }

    [Fact]
    public void The_day_is_the_readers_own_rather_than_the_clocks()
    {
        var store = new InMemoryUpcomingHorizonStore();
        var horizon = new UpcomingHorizon(store);
        horizon.SetDays(1);

        // 23:00 UTC is already the next day in this zone, so a day ahead of it reaches the day after
        // that - counted in UTC it would stop a day short and drop tomorrow evening off the card.
        var lateInTheEvening = DateTimeOffset.Parse("2026-08-27T23:00:00Z");
        Assert.True(horizon.Holds(DateTimeOffset.Parse("2026-08-29T18:00:00Z"), lateInTheEvening, Zone));
    }
}
