using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The line under every card on the notes screen. Orbit.Web has said it in as few words as it takes
/// all along; the phone printed the whole timestamp, which is the one line on a card nobody reads.
/// </summary>
public sealed class LastChangedTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Something_changed_earlier_today_says_so()
        => Assert.Equal("Today", Describe(Now.AddHours(-3)));

    [Fact]
    public void And_the_day_before_is_named_rather_than_dated()
        => Assert.Equal("Yesterday", Describe(Now.AddDays(-1)));

    /// <summary>
    /// Within the week a weekday reads faster than a date, and there is only one of each to mean.
    /// </summary>
    [Fact]
    public void Within_the_week_it_is_the_weekday()
        => Assert.Equal(
            Now.AddDays(-3).ToLocalTime().ToString("dddd", Words().DisplayCulture),
            Describe(Now.AddDays(-3)));

    /// <summary>
    /// Past a week a weekday is ambiguous - "Tuesday" could be any of them - so it becomes a date.
    /// </summary>
    [Fact]
    public void Past_the_week_it_is_a_date()
        => Assert.Equal(
            Now.AddDays(-9).ToLocalTime().ToString("d", Words().DisplayCulture),
            Describe(Now.AddDays(-9)));

    /// <summary>
    /// A row stamped ahead of now - a clock put back, or a device running fast - is dated rather than
    /// given a weekday nobody can place. The subtraction is signed, so this is the case that would
    /// otherwise fall through the week's own range by accident.
    /// </summary>
    [Fact]
    public void Something_dated_ahead_of_now_is_a_date()
        => Assert.Equal(
            Now.AddDays(2).ToLocalTime().ToString("d", Words().DisplayCulture),
            Describe(Now.AddDays(2)));

    private static string Describe(DateTimeOffset updatedAtUtc)
        => LastChanged.Describe(updatedAtUtc, Now, Words());

    private static Translations Words() => new(new InMemoryLanguageStore());
}
