using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Places;
using Orbit.Core.Transfer;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// The export's Places box, which is the one part of the file written out decrypted. What these hold in
/// place: it is offered, it waits to be asked for rather than coming along with an export pressed the
/// way it always was, the page says plainly what ticking it costs, and the file that comes out really
/// does carry a sealed place opened.
///
/// Everything the page reads on the way in other than the export is answered 404, which the page treats
/// as "couldn't load" and carries on - this is about the export section, not the rest of the page.
/// </summary>
public sealed class OptionsExportTests : OrbitTestContext
{
    private static readonly Guid OwnUserId = Guid.NewGuid();

    /// <summary>The archive the server answers with: one note, and one place sealed as the server holds it.</summary>
    private static readonly OrbitArchive ServersArchive = new(
        OrbitArchive.CurrentVersion, DateTimeOffset.UtcNow,
        [new ArchivedNote("Shopping", [new ArchivedNoteLine("Milk", false, false)], IsPrivate: false, EncryptedContent: null)],
        [], [], [],
        [new ArchivedPlace(
            string.Empty, string.Empty, new ArchivedEventLocation(string.Empty, 0, 0), "", "Normal", [],
            IsPrivate: true, new ArchivedEncryptedContent("c2VhbGVk", "bm9uY2U="))]);

    private readonly BunitJSModuleInterop _download;
    private readonly BunitJSModuleInterop _crypto;

    public OptionsExportTests()
    {
        // The rest of the page reaches for push, the theme and the accent through JS on the way in.
        // None of that is what these are about, so it answers with defaults - the two modules the
        // export itself uses are planned below, and their calls are what gets asserted.
        JSInterop.Mode = JSRuntimeMode.Loose;
        _download = JSInterop.SetupModule("./js/fileDownload.js");
        _download.SetupVoid("downloadText", _ => true).SetVoidResult();
        _crypto = JSInterop.SetupModule("./js/e2eeChat.js");
        _crypto.Setup<string?>("decryptForSelf", _ => true).SetResult(JsonSerializer.Serialize(new SealedPlace(
            "The spare key", "Under the third pot", new EventLocationDto("Piękna 1, Warszawa", 52.2297, 21.0122))));
        RegisterThePagesServices();
    }

    [Fact]
    public void Places_are_offered_but_wait_to_be_asked_for()
    {
        var cut = RenderComponent<Options>();

        Assert.False(PlacesBox(cut).HasAttribute("checked"));
        Assert.True(ExportBox(cut, "Notes").HasAttribute("checked"));
        Assert.Empty(cut.FindAll(".export-choices .field-hint-mark.warns"));
    }

    [Fact]
    public void Ticking_places_says_the_file_will_be_readable_by_anyone()
    {
        var cut = RenderComponent<Options>();

        PlacesBox(cut).Change(true);

        // A "!" beside Places rather than a line of prose under the choices.
        Assert.Equal("!", cut.Find(".export-choices .field-hint-mark.warns").TextContent);
        var warning = cut.Find(".export-choices .field-hint-bubble").TextContent;
        Assert.Contains("decrypted", warning);
        Assert.Contains("private ones included", warning);
    }

    /// <summary>
    /// The whole point: the server hands over a sealed place as empty words, and the file the reader
    /// saves carries its name, address and point.
    /// </summary>
    [Fact]
    public void An_export_with_places_writes_a_sealed_place_opened()
    {
        var cut = RenderComponent<Options>();
        PlacesBox(cut).Change(true);

        ExportButton(cut).Click();

        var written = WrittenArchive(cut);
        var place = Assert.Single(written.AllPlaces);
        Assert.Equal("The spare key", place.Name);
        Assert.Equal("Piękna 1, Warszawa", place.Where.Address);
        Assert.Equal(52.2297, place.Where.Latitude);
        Assert.Contains("1 places", cut.Markup);
    }

    [Fact]
    public void An_export_pressed_as_it_always_was_carries_no_places_and_opens_nothing()
    {
        var cut = RenderComponent<Options>();

        ExportButton(cut).Click();

        var written = WrittenArchive(cut);
        Assert.Single(written.Notes);
        Assert.Empty(written.AllPlaces);
        Assert.Empty(_crypto.Invocations["decryptForSelf"]);
    }

    private OrbitArchive WrittenArchive(IRenderedComponent<Options> cut)
    {
        cut.WaitForAssertion(() => Assert.NotEmpty(_download.Invocations["downloadText"]));
        var json = (string)_download.Invocations["downloadText"].Single().Arguments[2]!;
        return JsonSerializer.Deserialize<OrbitArchive>(json)!;
    }

    private static IElement PlacesBox(IRenderedComponent<Options> cut) => ExportBox(cut, "Places");

    private static IElement ExportBox(IRenderedComponent<Options> cut, string part)
        => cut.FindAll(".export-choices label").Single(label => label.TextContent.Trim() == part).QuerySelector("input")!;

    private static IElement ExportButton(IRenderedComponent<Options> cut)
        => cut.FindAll("button").Single(button => button.TextContent.Trim() == "Export");

    private void RegisterThePagesServices()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/api/transfer/export", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(ServersArchive) }
                : new HttpResponseMessage(HttpStatusCode.NotFound)))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        // Signed in, because opening a sealed place needs to know whose key to open it with.
        var tokenStore = new TokenStore(new StubJSRuntime());
        tokenStore.SetTokenAsync(AnUnsignedTokenFor(OwnUserId)).GetAwaiter().GetResult();
        var authenticationStateProvider = new OrbitAuthenticationStateProvider(
            tokenStore, new TokenRefreshService(tokenStore, httpClient));
        var usersApiClient = new UsersApiClient(httpClient);
        var ownEncryptionKeyProvider = new OwnEncryptionKeyProvider(
            JSInterop.JSRuntime, usersApiClient, authenticationStateProvider);

        Services.AddSingleton(authenticationStateProvider);
        Services.AddSingleton(usersApiClient);
        Services.AddSingleton(ownEncryptionKeyProvider);
        Services.AddSingleton(new ThemeService(JSInterop.JSRuntime));
        Services.AddSingleton(new AccentColorService(JSInterop.JSRuntime));
        Services.AddSingleton(new PushNotificationManager(JSInterop.JSRuntime, new PushNotificationApiClient(httpClient)));
        Services.AddSingleton(new NotificationsApiClient(httpClient));
        Services.AddSingleton(new ClientFlagsApiClient(httpClient));
        Services.AddSingleton(new GoogleIntegrationAccess(
            usersApiClient, new DevicePreferences(new StubJSRuntime()), NullLogger<GoogleIntegrationAccess>.Instance));
        Services.AddSingleton(new AuthApiClient(httpClient, tokenStore));
        Services.AddSingleton(new DevicePreferences(new StubJSRuntime()));
        Services.AddSingleton(new UserPermissionState(usersApiClient));
        Services.AddSingleton(new TransferApiClient(
            httpClient,
            new PrivateContentSealer(ownEncryptionKeyProvider, authenticationStateProvider, JSInterop.JSRuntime)));
    }

    private static string AnUnsignedTokenFor(Guid userId)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes($$"""{"sub":"{{userId}}"}"""));
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
