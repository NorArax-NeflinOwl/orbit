using System.Globalization;
using Bunit;
using Orbit.Web.Components;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// Orbit's own date and time boxes. The browser's own draw themselves in the browser's locale, which
/// gave a Polish page an AM/PM clock and a Sunday-first month - so these two decide the format and the
/// week themselves, and that is what these tests pin.
/// </summary>
public sealed class DateAndTimeFieldTests : OrbitTestContext
{
    [Fact]
    public void A_time_is_shown_on_a_24_hour_clock()
    {
        var cut = RenderComponent<TimeField>(parameters => parameters.Add(field => field.Value, new TimeOnly(17, 30)));

        Assert.Equal("17:30", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void A_time_typed_without_its_colon_is_still_understood()
    {
        TimeOnly? chosen = null;
        var cut = RenderComponent<TimeField>(parameters => parameters
            .Add(field => field.Value, new TimeOnly(9, 0))
            .Add(field => field.ValueChanged, value => chosen = value));

        cut.Find("input").Change("1745");

        Assert.Equal(new TimeOnly(17, 45), chosen);
    }

    [Fact]
    public void An_emptied_time_box_means_no_time()
    {
        TimeOnly? chosen = new TimeOnly(9, 0);
        var cut = RenderComponent<TimeField>(parameters => parameters
            .Add(field => field.Value, new TimeOnly(9, 0))
            .Add(field => field.ValueChanged, value => chosen = value));

        cut.Find("input").Change(string.Empty);

        Assert.Null(chosen);
    }

    [Fact]
    public void Something_that_is_not_a_time_is_refused_rather_than_guessed_at()
    {
        var reported = 0;
        var cut = RenderComponent<TimeField>(parameters => parameters
            .Add(field => field.Value, new TimeOnly(9, 0))
            .Add(field => field.ValueChanged, _ => reported++));

        cut.Find("input").Change("half past nine");

        Assert.Equal(0, reported);
        Assert.Equal("09:00", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void A_date_is_written_the_way_the_rest_of_the_app_writes_one()
    {
        var cut = RenderComponent<DateField>(parameters => parameters.Add(field => field.Value, new DateOnly(2026, 3, 9)));

        Assert.Equal("09.03.2026", cut.Find(".date-field-input").GetAttribute("value"));
    }

    [Fact]
    public void The_calendar_starts_on_Monday()
    {
        var cut = RenderComponent<DateField>(parameters => parameters.Add(field => field.Value, new DateOnly(2026, 3, 9)));

        cut.Find(".date-field-toggle").Click();

        // Named from the reader's own culture, so the assertion asks that culture rather than spelling
        // the names out - which ICU shortens differently between versions.
        var shortestDayNames = CultureInfo.GetCultureInfo("en-US").DateTimeFormat.ShortestDayNames;
        var weekdays = cut.FindAll(".date-field-weekdays span").Select(day => day.TextContent).ToArray();
        Assert.Equal(7, weekdays.Length);
        Assert.Equal(shortestDayNames[(int)DayOfWeek.Monday], weekdays[0]);
        Assert.Equal(shortestDayNames[(int)DayOfWeek.Sunday], weekdays[^1]);
    }

    [Fact]
    public void The_first_row_reaches_back_to_the_Monday_before_the_first()
    {
        // March 2026 starts on a Sunday, so the grid's first row runs 23 February to 1 March.
        var cut = RenderComponent<DateField>(parameters => parameters.Add(field => field.Value, new DateOnly(2026, 3, 9)));

        cut.Find(".date-field-toggle").Click();

        var days = cut.FindAll(".date-field-day").Select(day => day.TextContent.Trim()).ToArray();
        Assert.Equal("23", days[0]);
        Assert.Equal(0, days.Length % 7);
    }

    [Fact]
    public void Picking_a_day_answers_with_it_and_shuts_the_calendar()
    {
        DateOnly? chosen = null;
        var cut = RenderComponent<DateField>(parameters => parameters
            .Add(field => field.Value, new DateOnly(2026, 3, 9))
            .Add(field => field.ValueChanged, value => chosen = value));
        cut.Find(".date-field-toggle").Click();

        cut.FindAll(".date-field-day").First(day => day.TextContent.Trim() == "17").Click();

        Assert.Equal(new DateOnly(2026, 3, 17), chosen);
        Assert.Empty(cut.FindAll(".date-field-popup"));
    }

    [Fact]
    public void Something_that_is_not_a_date_is_refused_rather_than_guessed_at()
    {
        var reported = 0;
        var cut = RenderComponent<DateField>(parameters => parameters
            .Add(field => field.Value, new DateOnly(2026, 3, 9))
            .Add(field => field.ValueChanged, _ => reported++));

        cut.Find(".date-field-input").Change("next tuesday");

        Assert.Equal(0, reported);
        Assert.Equal("09.03.2026", cut.Find(".date-field-input").GetAttribute("value"));
    }
}
