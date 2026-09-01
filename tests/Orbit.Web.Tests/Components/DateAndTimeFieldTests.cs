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

    /// <summary>
    /// The colon is written in as the digits arrive, so a time reads as a time while it is being typed
    /// rather than only once the box is left.
    /// </summary>
    [Theory]
    [InlineData("1", "1")]
    [InlineData("14", "14")]
    [InlineData("143", "14:3")]
    [InlineData("1430", "14:30")]
    [InlineData("14305", "14:30")]
    public void The_colon_is_written_in_while_a_time_is_typed(string typed, string shown)
    {
        var cut = RenderComponent<TimeField>();

        cut.Find("input").Input(typed);

        Assert.Equal(shown, cut.Find("input").GetAttribute("value"));
    }

    /// <summary>
    /// Two digits keep no colon after them. Written as soon as the second one lands, a backspace would
    /// put it straight back and the box could not be cleared past it.
    /// </summary>
    [Fact]
    public void Backspacing_past_the_colon_is_not_undone()
    {
        var cut = RenderComponent<TimeField>();
        cut.Find("input").Input("143");

        cut.Find("input").Input("14");

        Assert.Equal("14", cut.Find("input").GetAttribute("value"));
    }

    /// <summary>Anything that is not a digit is dropped rather than left to fail the parse on leaving.</summary>
    [Fact]
    public void Only_the_digits_of_what_was_typed_are_kept()
    {
        var cut = RenderComponent<TimeField>();

        cut.Find("input").Input("1a4-3o0");

        Assert.Equal("14:30", cut.Find("input").GetAttribute("value"));
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
