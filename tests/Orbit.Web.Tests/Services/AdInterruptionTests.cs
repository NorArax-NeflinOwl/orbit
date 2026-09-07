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
    [Fact]
    public void An_ordinary_account_is_shown_it()
        => Assert.True(AdInterruption.ShouldShow(PermissionsHolding("[]"), hasShownItThisVisit: false));

    /// <summary>
    /// Once a visit. An advert that came back on every navigation is the thing that makes people leave,
    /// and this is the flag that stops it.
    /// </summary>
    [Fact]
    public void It_is_not_shown_twice_in_one_visit()
        => Assert.False(AdInterruption.ShouldShow(PermissionsHolding("[]"), hasShownItThisVisit: true));

    /// <summary>
    /// Never to somebody working on Orbit rather than reading it: the Debugger permission is what says
    /// which of the two this account is, and an advert over the top of a captured log interrupts without
    /// having anything to offer.
    /// </summary>
    [Fact]
    public void An_account_holding_the_Debugger_permission_is_never_interrupted()
        => Assert.False(AdInterruption.ShouldShow(PermissionsHolding("[\"Debug\"]"), hasShownItThisVisit: false));

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
