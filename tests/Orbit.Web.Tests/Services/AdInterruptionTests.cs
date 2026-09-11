using System.Net;
using System.Text;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Whether the advert that interrupts is shown - the only thing about it worth arguing over, which is
/// why it is a rule of its own rather than a condition inside the layout. See AdInterruption, and
/// AdAudience for the half of the answer every advertising surface shares.
/// </summary>
public sealed class AdInterruptionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A browser that has never shown one, and one that has not been allowed to remember.</summary>
    [Fact]
    public async Task An_ordinary_account_that_has_not_had_one_is_shown_it()
        => Assert.True(AdInterruption.ShouldShow(await AudienceAsync("[]"), lastShownAtUtc: null, Now));

    /// <summary>
    /// The whole point of the gap. It used to be a flag on the page saying "this visit has had it",
    /// which ended at the next refresh - so reloading was a way of asking for the advert again, and
    /// reloading is what people do.
    /// </summary>
    [Fact]
    public async Task It_is_not_shown_again_inside_the_gap()
        => Assert.False(AdInterruption.ShouldShow(
            await AudienceAsync("[]"), Now - AdInterruption.MinimumGap + TimeSpan.FromSeconds(1), Now));

    /// <summary>And it comes back once the gap has passed - it is a pace rather than a once.</summary>
    [Fact]
    public async Task It_is_shown_again_once_the_gap_has_passed()
        => Assert.True(AdInterruption.ShouldShow(
            await AudienceAsync("[]"), Now - AdInterruption.MinimumGap, Now));

    /// <summary>
    /// Not to somebody working on Orbit rather than reading it, until they ask: the Debugger permission is
    /// what says which of the two this account is, and "Allow ads" is off for it by default.
    /// </summary>
    [Fact]
    public async Task An_account_holding_the_Debugger_permission_is_not_interrupted_by_default()
        => Assert.False(AdInterruption.ShouldShow(await AudienceAsync("[\"Debug\"]"), lastShownAtUtc: null, Now));

    /// <summary>The switch is what lets the people who make the adverts see them the way everybody else does.</summary>
    [Fact]
    public async Task An_account_holding_the_Debugger_permission_is_interrupted_once_it_allows_ads()
        => Assert.True(AdInterruption.ShouldShow(
            await AudienceAsync("[\"Debug\"]", allowAdsForDebugger: true), lastShownAtUtc: null, Now));

    /// <summary>
    /// The switch is asked about nobody else: an ordinary account on a browser where somebody once turned
    /// it off still sees adverts - it cannot be used to opt out of them without the permission.
    /// </summary>
    [Fact]
    public async Task The_switch_means_nothing_to_an_account_without_the_Debugger_permission()
    {
        var audience = await AudienceAsync("[]", allowAdsForDebugger: false);

        Assert.True(audience.MayShowAds);
    }

    /// <summary>
    /// A slot already on screen has to hear that the answer changed - flipping the switch under Options
    /// re-renders Options and nothing else.
    /// </summary>
    [Fact]
    public async Task Flipping_the_switch_is_announced()
    {
        var preferences = new DevicePreferences(new StubJSRuntime());
        var audience = new AdAudience(await PermissionsHoldingAsync("[\"Debug\"]"), preferences);
        var announced = 0;
        audience.Changed += () => announced++;

        await preferences.SetAllowAdsForDebuggerAsync(true);

        Assert.Equal(1, announced);
        Assert.True(audience.MayShowAds);
    }

    private static async Task<AdAudience> AudienceAsync(string grantedJson, bool allowAdsForDebugger = false)
    {
        var preferences = new DevicePreferences(new StubJSRuntime());
        await preferences.SetAllowAdsForDebuggerAsync(allowAdsForDebugger);
        return new AdAudience(await PermissionsHoldingAsync(grantedJson), preferences);
    }

    private static async Task<UserPermissionState> PermissionsHoldingAsync(string grantedJson)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"granted\":" + grantedJson + "}", Encoding.UTF8, "application/json")
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var permissions = new UserPermissionState(new UsersApiClient(httpClient));
        await permissions.RefreshAsync();
        return permissions;
    }
}
