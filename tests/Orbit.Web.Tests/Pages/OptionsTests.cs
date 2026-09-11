using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Config;
using Orbit.Contracts.Notifications;
using Orbit.Contracts.Users;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// The Options page, driven against a server double that answers the handful of requests the page makes
/// while it loads. Only what the tests below are about is modelled, and modelled the way the server
/// decides it - see <see cref="Answer"/>.
/// </summary>
public sealed class OptionsTests : OrbitTestContext
{
    private string _grantedJson = "[]";

    public OptionsTests()
    {
        // The page asks the browser about push support, the theme and the file picker while it loads;
        // none of that is what these tests are about, and each answers "nothing" in loose mode.
        JSInterop.Mode = JSRuntimeMode.Loose;

        var httpClient = new HttpClient(new StubHttpMessageHandler(Answer))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var usersApiClient = new UsersApiClient(httpClient);
        var devicePreferences = new DevicePreferences(JSInterop.JSRuntime);
        var permissions = new UserPermissionState(usersApiClient);

        Services.AddSingleton(usersApiClient);
        Services.AddSingleton(devicePreferences);
        Services.AddSingleton(permissions);
        Services.AddSingleton(new AdAudience(permissions, devicePreferences));
        Services.AddSingleton(new ThemeService(JSInterop.JSRuntime));
        Services.AddSingleton(new AccentColorService(JSInterop.JSRuntime));
        Services.AddSingleton(new PushNotificationManager(JSInterop.JSRuntime, new PushNotificationApiClient(httpClient)));
        Services.AddSingleton(new NotificationsApiClient(httpClient));
        Services.AddSingleton(new ClientFlagsApiClient(httpClient));
        Services.AddSingleton(new GoogleIntegrationAccess(
            usersApiClient, devicePreferences, NullLogger<GoogleIntegrationAccess>.Instance));
        Services.AddSingleton(new TransferApiClient(httpClient));
        Services.AddSingleton(new AuthApiClient(httpClient, new TokenStore(JSInterop.JSRuntime)));
        Services.AddSingleton(services => new OwnEncryptionKeyProvider(
            JSInterop.JSRuntime, usersApiClient, services.GetRequiredService<OrbitAuthenticationStateProvider>()));
    }

    /// <summary>The account the page loads. Google-linked, so the Google row shows its Disconnect button rather than Google's own.</summary>
    private AccountDto Account { get; set; } = new(
        Guid.NewGuid(), "gina@example.com", "gina", "Gina", IsEmailVerified: true, HasPassword: false, IsGoogleLinked: true);

    [Fact]
    public void The_ads_switch_is_not_offered_to_an_account_without_Debugger()
    {
        var cut = RenderComponent<Options>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".options-tab")));
        Assert.DoesNotContain(cut.FindAll(".options-tab"), tab => tab.TextContent.Trim() == "Debug");
        Assert.Empty(cut.FindAll("#allowAdsSwitch"));
    }

    /// <summary>
    /// Off by default, which is the point of it: whoever holds Debugger is working on Orbit, and the
    /// adverts were in the way of that.
    /// </summary>
    [Fact]
    public void For_an_account_with_Debugger_the_switch_starts_off_and_no_ads_are_shown()
    {
        _grantedJson = "[\"Debug\"]";

        var cut = RenderOnTheDebugTab();

        Assert.DoesNotContain("on", cut.Find("#allowAdsSwitch").ClassList);
        Assert.False(Services.GetRequiredService<AdAudience>().MayShowAds);
    }

    [Fact]
    public void Turning_the_switch_on_lets_the_ads_back_for_that_account()
    {
        _grantedJson = "[\"Debug\"]";
        var cut = RenderOnTheDebugTab();

        cut.Find("#allowAdsSwitch").Click();

        Assert.Contains("on", cut.Find("#allowAdsSwitch").ClassList);
        Assert.True(Services.GetRequiredService<DevicePreferences>().AllowAdsForDebugger);
        Assert.True(Services.GetRequiredService<AdAudience>().MayShowAds);
    }

    /// <summary>Nothing changes for everybody else: an ordinary account sees adverts exactly as before.</summary>
    [Fact]
    public void An_account_without_Debugger_is_shown_ads_whatever_the_switch_says()
    {
        var cut = RenderComponent<Options>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".options-tab")));
        Assert.False(Services.GetRequiredService<DevicePreferences>().AllowAdsForDebugger);
        Assert.True(Services.GetRequiredService<AdAudience>().MayShowAds);
    }

    private IRenderedComponent<Options> RenderOnTheDebugTab()
    {
        var cut = RenderComponent<Options>();
        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll(".options-tab"), tab => tab.TextContent.Trim() == "Debug"));
        cut.FindAll(".options-tab").Single(tab => tab.TextContent.Trim() == "Debug").Click();
        return cut;
    }

    /// <summary>
    /// The requests Options makes while it loads, answered the way Orbit.Api answers them. Anything else
    /// is a 404, so a request nobody expected fails loudly rather than being quietly agreed with.
    /// </summary>
    private HttpResponseMessage Answer(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        return (request.Method.Method, path) switch
        {
            ("GET", "/api/users/me/permissions") => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"granted\":" + _grantedJson + "}", Encoding.UTF8, "application/json")
            },
            ("GET", "/api/users/me") => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Account) },
            ("GET", "/api/notifications/settings") => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new NotificationSettingsDto(
                    true, true, true, true, false, BannerVisibleSeconds: 5, BannerMinimumGapSeconds: 5))
            },
            ("GET", "/api/config/client-flags") => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ClientFlagsDto(
                    ExceptionDetailsAllowed: false, GoogleClientId: string.Empty, WebAddress: string.Empty,
                    GoogleAndroidClientId: string.Empty, GoogleIosClientId: string.Empty))
            },
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        };
    }
}
