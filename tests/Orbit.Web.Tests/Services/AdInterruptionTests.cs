using System.Net;
using System.Text;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Whether the advert that interrupts is shown - the only thing about it worth arguing over, which is
/// why it is a rule of its own rather than a condition inside the layout. See AdInterruption.
/// </summary>
public sealed class AdInterruptionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A browser that has never shown one, and one that has not been allowed to remember.</summary>
    [Fact]
    public void An_ordinary_account_that_has_not_had_one_is_shown_it()
        => Assert.True(AdInterruption.ShouldShow(PermissionsHolding("[]"), lastShownAtUtc: null, Now));

    /// <summary>
    /// The whole point of the gap. It used to be a flag on the page saying "this visit has had it",
    /// which ended at the next refresh - so reloading was a way of asking for the advert again, and
    /// reloading is what people do.
    /// </summary>
    [Fact]
    public void It_is_not_shown_again_inside_the_gap()
        => Assert.False(AdInterruption.ShouldShow(
            PermissionsHolding("[]"), Now - AdInterruption.MinimumGap + TimeSpan.FromSeconds(1), Now));

    /// <summary>And it comes back once the gap has passed - it is a pace rather than a once.</summary>
    [Fact]
    public void It_is_shown_again_once_the_gap_has_passed()
        => Assert.True(AdInterruption.ShouldShow(
            PermissionsHolding("[]"), Now - AdInterruption.MinimumGap, Now));

    /// <summary>
    /// Never to somebody working on Orbit rather than reading it: the Debugger permission is what says
    /// which of the two this account is, and an advert over the top of a captured log interrupts without
    /// having anything to offer.
    /// </summary>
    [Fact]
    public void An_account_holding_the_Debugger_permission_is_never_interrupted()
        => Assert.False(AdInterruption.ShouldShow(PermissionsHolding("[\"Debug\"]"), lastShownAtUtc: null, Now));

    private static UserPermissionState PermissionsHolding(string grantedJson)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"granted\":" + grantedJson + "}", Encoding.UTF8, "application/json")
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var permissions = new UserPermissionState(new UsersApiClient(httpClient));
        permissions.RefreshAsync().GetAwaiter().GetResult();
        return permissions;
    }
}
