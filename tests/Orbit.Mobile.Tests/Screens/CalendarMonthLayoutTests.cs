using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// That the month grid is seven columns wide because it was built that way, not because seven happened
/// to fit.
///
/// The cells were a fixed 46 units in a wrapping layout, so how many days sat on a row depended on how
/// wide the screen was - and on a phone slightly wider than the one it was written on, the grid ran
/// eight days to a row. A month with eight-day weeks is not a rendering imperfection; it is the calendar
/// saying something false, and nothing a unit test could see.
///
/// Checked against the markup because that is where the mistake lives. CalendarMonthTests already proves
/// the data is six weeks of seven.
/// </summary>
public sealed class CalendarMonthLayoutTests
{
    /// <summary>A week. The number this whole file exists to defend.</summary>
    private const int DaysInAWeek = 7;

    [Fact]
    public void The_days_and_the_names_above_them_are_each_a_seventh_of_the_row()
    {
        var bases = Regex.Matches(ReadTheCalendar(), @"FlexLayout\.Basis=""([\d.]+)%""")
            .Select(match => double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToList();

        // The weekday names and the day cells - one each, and they have to agree or the columns do not
        // line up under their own headings.
        Assert.Equal(2, bases.Count);
        Assert.Equal(bases[0], bases[1]);
    }

    /// <summary>
    /// Seven of them have to fit, and an eighth must not. A basis a shade over a seventh - 14.29% - adds
    /// up to more than a row and wraps at six, which is the same bug in the other direction.
    /// </summary>
    [Fact]
    public void Seven_of_them_fit_a_row_and_an_eighth_does_not()
    {
        var basis = Regex.Match(ReadTheCalendar(), @"FlexLayout\.Basis=""([\d.]+)%""").Groups[1].Value;
        var share = double.Parse(basis, CultureInfo.InvariantCulture);

        Assert.True(share * DaysInAWeek <= 100, $"{DaysInAWeek} x {share}% is more than a row");
        Assert.True(share * (DaysInAWeek + 1) > 100, $"{DaysInAWeek + 1} x {share}% still fits a row");
    }

    /// <summary>
    /// The markup itself, found by walking up from the test binary rather than by a path relative to the
    /// working directory - which differs between a run from the IDE and one from the command line.
    /// </summary>
    private static string ReadTheCalendar()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(
            directory!.FullName, "src", "Clients", "Orbit.Maui", "Features", "Calendar", "CalendarPage.xaml"));
    }
}
