using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// What the avatar menu asks and what it offers. Two addresses that answer different questions - what
/// was logged, and what is happening this second - so the reader is asked which they want, but only
/// where there are actually two.
/// </summary>
public sealed class DiagnosticsDashboardTests
{
    private const string History = "https://portal.example/logs";
    private const string Live = "https://portal.example/logstream";

    [Fact]
    public void A_deployment_that_publishes_neither_offers_nothing()
    {
        var dashboard = new DiagnosticsDashboard(string.Empty, string.Empty);

        Assert.False(dashboard.HasAny);
        Assert.False(dashboard.HasBoth);
    }

    [Fact]
    public void Both_addresses_make_it_a_question()
    {
        var dashboard = new DiagnosticsDashboard(History, Live);

        Assert.True(dashboard.HasAny);
        Assert.True(dashboard.HasBoth);
    }

    /// <summary>A choice with one option is not a choice, so the entry opens the one there is.</summary>
    [Theory]
    [InlineData(History, "")]
    [InlineData("", Live)]
    public void One_address_is_opened_rather_than_offered(string history, string live)
    {
        var dashboard = new DiagnosticsDashboard(history, live);

        Assert.True(dashboard.HasAny);
        Assert.False(dashboard.HasBoth);
        Assert.Equal(history.Length > 0 ? history : live, dashboard.TheOnlyUrl);
    }
}
