using Orbit.Contracts.Calendar;
using Orbit.Mobile.Data;
using Orbit.Localization;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Calendar;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The year overview. Orbit.Web draws twelve day grids; a phone is not wide enough, so this answers the
/// same question a different way - which months hold anything, and take me to one.
/// </summary>
public sealed class CalendarYearTests
{
    private static readonly DateTime August = new(2026, 8, 15);

    [Fact]
    public void A_year_is_twelve_months_in_order()
    {
        var months = CalendarYear.Build(2026, August, [], [], English());

        Assert.Equal(12, months.Count);
        Assert.Equal(new DateTime(2026, 1, 1), months[0].Month);
        Assert.Equal(new DateTime(2026, 12, 1), months[11].Month);
    }

    [Fact]
    public void Each_month_says_how_much_is_in_it()
    {
        var months = CalendarYear.Build(
            2026, August,
            [EventOn(new DateTime(2026, 8, 3, 9, 0, 0)), EventOn(new DateTime(2026, 8, 20, 9, 0, 0)),
             EventOn(new DateTime(2026, 11, 2, 9, 0, 0))], [],
            English());

        Assert.Equal(2, months[7].EventCount);
        Assert.True(months[7].HasEvents);
        Assert.Equal(1, months[10].EventCount);
        Assert.All(months.Where(month => month.Month.Month is not (8 or 11)), month => Assert.False(month.HasEvents));
    }

    /// <summary>
    /// A month holding nothing but deadlines is not an empty month - the overview is asked which months
    /// have anything in them, and something due is something in it.
    /// </summary>
    [Fact]
    public void A_month_with_only_deadlines_still_has_something_in_it()
    {
        var months = CalendarYear.Build(
            2026, August, [], [DueOn(new DateTime(2026, 11, 2, 17, 0, 0))], English());

        Assert.True(months[10].HasEvents);
    }

    private static CalendarDeadline DueOn(DateTime localDue)
        => new(
            Guid.NewGuid(), Guid.NewGuid(), "Groceries", "Buy milk", localDue.Date, localDue.ToString("g"),
            IsCompleted: false, IsSomewhere: false);

    /// <summary>
    /// The same date in a neighbouring year must not be counted here, which is the mistake a grouping
    /// by month alone would make.
    /// </summary>
    [Fact]
    public void Another_years_events_are_not_counted()
    {
        var months = CalendarYear.Build(
            2026, August, [EventOn(new DateTime(2025, 8, 20, 9, 0, 0)), EventOn(new DateTime(2027, 8, 20, 9, 0, 0))], [],
            English());

        Assert.All(months, month => Assert.False(month.HasEvents));
    }

    [Fact]
    public void The_month_we_are_in_is_marked_and_only_in_its_own_year()
    {
        Assert.True(CalendarYear.Build(2026, August, [], [], English())[7].IsThisMonth);
        Assert.All(CalendarYear.Build(2027, August, [], [], English()), month => Assert.False(month.IsThisMonth));
    }

    /// <summary>Polish writes month names lower-case; a grid of headings reads better capitalised.</summary>
    [Fact]
    public void Month_names_are_capitalised_in_the_readers_own_calendar()
    {
        Assert.Equal("Styczeń", CalendarYear.Build(2026, August, [], [], Polish())[0].Name);
        Assert.Equal("January", CalendarYear.Build(2026, August, [], [], English())[0].Name);
    }

    private static Translations English() => new(new InMemoryLanguageStore());

    private static Translations Polish()
    {
        var translations = new Translations(new InMemoryLanguageStore());
        translations.SetLanguage(AppLanguage.Polish);
        return translations;
    }

    private static LocalCalendarEvent EventOn(DateTime localStart)
    {
        var start = new DateTimeOffset(localStart, TimeZoneInfo.Local.GetUtcOffset(localStart));

        return new LocalCalendarEvent
        {
            LocalId = Guid.NewGuid(),
            Details = new CalendarEventDetailsDto(
                "Meeting", null, null, null, start.ToUniversalTime(), start.AddHours(1).ToUniversalTime(),
                false, null, [], [], "None", "None")
        };
    }
}
