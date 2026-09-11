using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bunit;
using Microsoft.AspNetCore.Components;
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

    /// <summary>
    /// The password the server holds for the account, or null for one that has none - which is what
    /// decides whether DELETE /api/users/me is refused, exactly as DeleteAccountCommandHandler decides it.
    /// </summary>
    private string? ServerPassword { get; set; }

    /// <summary>Every body DELETE /api/users/me was sent, refused or not.</summary>
    private readonly List<DeleteAccountRequest> _deletionRequests = [];

    private bool _accountDeleted;

    /// <summary>
    /// This deployment's Google client id - none unless a test configures one, which leaves the typed
    /// confirmation as the passwordless account's way to delete itself.
    /// </summary>
    private string _googleClientId = string.Empty;

    /// <summary>The one token the server double takes for a fresh Google sign-in of this account.</summary>
    private const string FreshGoogleToken = "fresh-google-token";

    /// <summary>
    /// Where Google can be asked, a passwordless account confirms with it: Google's button in place of the
    /// typed address, and in place of the Delete button too - pressing Google's is what deletes.
    /// </summary>
    [Fact]
    public void Where_google_is_configured_a_passwordless_account_confirms_with_it()
    {
        _googleClientId = "web-client-id";

        var cut = RenderComponent<Options>();

        cut.WaitForAssertion(() => cut.Find("#deleteAccountWithGoogle"));
        Assert.Empty(cut.FindAll("#deleteAccountConfirmationInput"));
        Assert.Empty(cut.FindAll(".btn-danger"));
    }

    [Fact]
    public async Task A_fresh_google_sign_in_deletes_the_passwordless_account()
    {
        _googleClientId = "web-client-id";
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        var cut = RenderComponent<Options>();
        cut.WaitForAssertion(() => cut.Find("#deleteAccountWithGoogle"));

        await cut.InvokeAsync(() => DeletionsGoogleButton(cut).Instance.OnGoogleCredential(FreshGoogleToken));

        cut.WaitForAssertion(() => Assert.True(_accountDeleted));
        Assert.Equal(FreshGoogleToken, Assert.Single(_deletionRequests).GoogleIdToken);
        Assert.EndsWith("/login", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public async Task A_google_sign_in_the_server_refuses_leaves_the_account_and_says_so()
    {
        _googleClientId = "web-client-id";
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        var cut = RenderComponent<Options>();
        cut.WaitForAssertion(() => cut.Find("#deleteAccountWithGoogle"));

        await cut.InvokeAsync(() => DeletionsGoogleButton(cut).Instance.OnGoogleCredential("an-old-token"));

        cut.WaitForAssertion(() => Assert.Contains("Google didn't confirm this account. Try again.", cut.Markup));
        Assert.False(_accountDeleted);
    }

    /// <summary>Declining the confirm() after Google stops it as it stops every other way in.</summary>
    [Fact]
    public async Task Declining_the_confirmation_after_google_still_stops_it()
    {
        _googleClientId = "web-client-id";
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(false);
        var cut = RenderComponent<Options>();
        cut.WaitForAssertion(() => cut.Find("#deleteAccountWithGoogle"));

        await cut.InvokeAsync(() => DeletionsGoogleButton(cut).Instance.OnGoogleCredential(FreshGoogleToken));

        Assert.Empty(_deletionRequests);
    }

    /// <summary>The Google button inside the danger zone - the linked account shows no other one.</summary>
    private static IRenderedComponent<Orbit.Web.Components.GoogleSignInButton> DeletionsGoogleButton(IRenderedComponent<Options> cut)
        => cut.FindComponents<Orbit.Web.Components.GoogleSignInButton>().Single();

    /// <summary>
    /// An account that signs in with Google and has a password anyway - chat made it set one. The form
    /// asks for it, and has to say which password it means and where to go when it is forgotten: a bare
    /// "Password" was a dead end for somebody who has only ever pressed the Google button.
    /// </summary>
    [Fact]
    public void A_google_account_with_a_password_is_told_which_password_and_where_to_reset_it()
    {
        Account = Account with { HasPassword = true };
        ServerPassword = "chat-password";

        var cut = RenderComponent<Options>();

        cut.WaitForAssertion(() => cut.Find("#deleteAccountPasswordInput"));
        Assert.Contains("besides Google", cut.Find("label[for=deleteAccountPasswordInput]").TextContent);
        Assert.Equal("/forgot-password", cut.Find("#deleteAccountForgotPassword").GetAttribute("href"));
    }

    /// <summary>
    /// An account without a password has nothing the server checks, so the press has to be deliberate
    /// some other way: nothing is sent until the address or the login has been typed.
    /// </summary>
    [Fact]
    public void A_passwordless_account_is_not_deleted_without_typing_its_address()
    {
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        var cut = RenderComponent<Options>();
        cut.WaitForAssertion(() => cut.Find("#deleteAccountConfirmationInput"));

        cut.Find(".btn-danger").Click();

        Assert.Contains("That isn't this account's email address or login.", cut.Markup);
        Assert.Empty(_deletionRequests);
        Assert.Empty(cut.FindAll("#deleteAccountPasswordInput"));
    }

    [Fact]
    public void Typing_something_else_is_refused_before_anything_is_sent()
    {
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        var cut = RenderComponent<Options>();
        cut.WaitForAssertion(() => cut.Find("#deleteAccountConfirmationInput"));

        cut.Find("#deleteAccountConfirmationInput").Input("somebody@example.com");
        cut.Find(".btn-danger").Click();

        Assert.Contains("That isn't this account's email address or login.", cut.Markup);
        Assert.Empty(_deletionRequests);
    }

    /// <summary>
    /// The address in any case, which is how people type one. What is sent is still the empty password -
    /// the typing is checked here, and the request installed phones share is unchanged.
    /// </summary>
    [Fact]
    public void Typing_the_address_deletes_the_passwordless_account()
    {
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        var cut = RenderComponent<Options>();
        cut.WaitForAssertion(() => cut.Find("#deleteAccountConfirmationInput"));

        cut.Find("#deleteAccountConfirmationInput").Input("  GINA@example.com ");
        cut.Find(".btn-danger").Click();

        cut.WaitForAssertion(() => Assert.True(_accountDeleted));
        Assert.Equal(string.Empty, Assert.Single(_deletionRequests).Password);
        Assert.EndsWith("/login", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void The_login_confirms_as_well_as_the_address()
    {
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        var cut = RenderComponent<Options>();
        cut.WaitForAssertion(() => cut.Find("#deleteAccountConfirmationInput"));

        cut.Find("#deleteAccountConfirmationInput").Input("gina");
        cut.Find(".btn-danger").Click();

        cut.WaitForAssertion(() => Assert.True(_accountDeleted));
    }

    /// <summary>The typing comes before the confirm(), not instead of it: saying no there still stops it.</summary>
    [Fact]
    public void Declining_the_confirmation_still_stops_it()
    {
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(false);
        var cut = RenderComponent<Options>();
        cut.WaitForAssertion(() => cut.Find("#deleteAccountConfirmationInput"));

        cut.Find("#deleteAccountConfirmationInput").Input("gina@example.com");
        cut.Find(".btn-danger").Click();

        Assert.Empty(_deletionRequests);
        Assert.False(_accountDeleted);
    }

    /// <summary>The server refuses a wrong password, and the page says so rather than claiming it worked.</summary>
    [Fact]
    public void A_wrong_password_leaves_the_account_where_it_was()
    {
        Account = Account with { HasPassword = true };
        ServerPassword = "chat-password";
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        var cut = RenderComponent<Options>();
        cut.WaitForAssertion(() => cut.Find("#deleteAccountPasswordInput"));

        cut.Find("#deleteAccountPasswordInput").Change("not-it");
        cut.Find(".btn-danger").Click();

        cut.WaitForAssertion(() => Assert.Contains("That password isn't right.", cut.Markup));
        Assert.False(_accountDeleted);
    }

    [Fact]
    public void The_right_password_deletes_the_account_and_signs_out()
    {
        Account = Account with { HasPassword = true };
        ServerPassword = "chat-password";
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        var cut = RenderComponent<Options>();
        cut.WaitForAssertion(() => cut.Find("#deleteAccountPasswordInput"));

        cut.Find("#deleteAccountPasswordInput").Change("chat-password");
        cut.Find(".btn-danger").Click();

        cut.WaitForAssertion(() => Assert.True(_accountDeleted));
        Assert.EndsWith("/login", Services.GetRequiredService<NavigationManager>().Uri);
    }

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
            ("DELETE", "/api/users/me") => DeleteAccount(request),
            ("GET", "/api/notifications/settings") => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new NotificationSettingsDto(
                    true, true, true, true, false, BannerVisibleSeconds: 5, BannerMinimumGapSeconds: 5))
            },
            ("GET", "/api/config/client-flags") => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ClientFlagsDto(
                    ExceptionDetailsAllowed: false, GoogleClientId: _googleClientId, WebAddress: string.Empty,
                    GoogleAndroidClientId: string.Empty, GoogleIosClientId: string.Empty))
            },
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        };
    }

    /// <summary>
    /// DeleteAccountCommandHandler's rule, and no more generous: a Google sign-in that was sent decides it
    /// (only a fresh one for a linked account passes), otherwise an account with a password is refused
    /// unless the one sent matches, and one without needs none. A body missing altogether is refused the
    /// way the binder refuses it.
    /// </summary>
    private HttpResponseMessage DeleteAccount(HttpRequestMessage request)
    {
        var body = request.Content?.ReadFromJsonAsync<DeleteAccountRequest>().GetAwaiter().GetResult();
        if (body is null)
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        }

        _deletionRequests.Add(body);
        if (body.GoogleIdToken is { Length: > 0 } idToken)
        {
            if (!Account.IsGoogleLinked || idToken != FreshGoogleToken)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }
        }
        else if (ServerPassword is { } expected && body.Password != expected)
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        _accountDeleted = true;
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }
}
