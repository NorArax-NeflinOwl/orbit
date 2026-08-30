using Orbit.Core.Inventory;
using Xunit;

namespace Orbit.Api.Tests.Inventory;

/// <summary>
/// How long something keeps, read back from the date that is stored and turned into one again.
///
/// The rule lives in Orbit.Core because both clients ask the question and both have to give the same
/// answer: a phone reading "14 days" where a browser reads "2 weeks" would be two apps disagreeing
/// about the same row.
/// </summary>
public sealed class ExpiryPeriodTests
{
    private static readonly DateTime Today = new(2026, 8, 30);

    [Fact]
    public void Nothing_stored_means_it_does_not_expire()
    {
        var period = ExpiryPeriod.For(null, Today);

        Assert.Equal(ExpiryUnit.None, period.Unit);
        Assert.Null(period.On(Today));
    }

    /// <summary>
    /// The coarsest unit that lands on it exactly, so a fortnight reads as one rather than fourteen -
    /// and a day count that no month or week lands on stays a day count.
    /// </summary>
    [Theory]
    [InlineData(1, 1, ExpiryUnit.Days)]
    [InlineData(15, 15, ExpiryUnit.Days)]
    [InlineData(14, 2, ExpiryUnit.Weeks)]
    [InlineData(60, 60, ExpiryUnit.Days)]
    public void A_stored_date_reads_back_in_the_coarsest_unit_that_fits(int days, int amount, ExpiryUnit unit)
    {
        var period = ExpiryPeriod.For(new DateTimeOffset(Today.AddDays(days), TimeSpan.Zero), Today);

        Assert.Equal(new ExpiryPeriod(amount, unit), period);
    }

    /// <summary>Zero would offer nothing to edit, so something already gone reads as a day.</summary>
    [Fact]
    public void A_date_already_gone_reads_as_one_day()
    {
        var period = ExpiryPeriod.For(new DateTimeOffset(Today.AddDays(-9), TimeSpan.Zero), Today);

        Assert.Equal(new ExpiryPeriod(1, ExpiryUnit.Days), period);
    }

    /// <summary>
    /// Counted from today rather than from whatever the item held before: "keeps two weeks" is a
    /// statement about something just put on the shelf, which is when anybody sets it.
    /// </summary>
    [Fact]
    public void A_length_lands_on_a_date_counted_from_today()
    {
        Assert.Equal(Today.AddDays(14), new ExpiryPeriod(2, ExpiryUnit.Weeks).On(Today)!.Value.Date);
        Assert.Equal(Today.AddMonths(3), new ExpiryPeriod(3, ExpiryUnit.Months).On(Today)!.Value.Date);
        Assert.Equal(Today.AddYears(1), new ExpiryPeriod(1, ExpiryUnit.Years).On(Today)!.Value.Date);
    }

    /// <summary>
    /// What the two clients hang on: a date written by one and read by the other has to come back as
    /// the same length it was set as.
    /// </summary>
    [Theory]
    [InlineData(2, ExpiryUnit.Weeks)]
    [InlineData(1, ExpiryUnit.Months)]
    [InlineData(3, ExpiryUnit.Months)]
    [InlineData(18, ExpiryUnit.Months)]
    [InlineData(1, ExpiryUnit.Years)]
    [InlineData(2, ExpiryUnit.Years)]
    [InlineData(5, ExpiryUnit.Days)]
    public void A_length_survives_being_stored_and_read_again(int amount, ExpiryUnit unit)
    {
        var stored = new ExpiryPeriod(amount, unit).On(Today);

        Assert.Equal(new ExpiryPeriod(amount, unit), ExpiryPeriod.For(stored, Today));
    }
}
