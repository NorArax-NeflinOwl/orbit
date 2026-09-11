using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bunit;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Places;
using Orbit.Core.Transfer;
using Orbit.Core.Transfer.ImportArchive;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// An export of places is the one part of the account's file written out opened: the server hands over
/// a sealed place as empty words beside its sealed half, and the browser that holds the key fills the
/// words in. What these hold in place is that the file really does carry them, that nothing asks for the
/// key when places were not chosen, and that the opened words never travel back up on an import.
///
/// The sealer is the real <see cref="PrivateContentSealer"/> over a stubbed <c>e2eeChat.js</c>, as in
/// PrivateTaskListItemIdTests: what is under test is what the archive ends up carrying, not the crypto.
/// The JS interop is strict, so a call to "decryptForSelf" nobody planned fails the test rather than
/// quietly answering.
/// </summary>
public sealed class ExportedPlacesTests : OrbitTestContext
{
    private static readonly Guid OwnUserId = Guid.NewGuid();

    /// <summary>A sealed place exactly as the server writes one into the archive - see ExportArchiveQueryHandler.</summary>
    private static readonly ArchivedPlace SealedAsTheServerHoldsIt = new(
        string.Empty, string.Empty, new ArchivedEventLocation(string.Empty, 0, 0), "#3366ff", "High", ["Viewing"],
        IsPrivate: true, new ArchivedEncryptedContent("c2VhbGVk", "bm9uY2U="));

    private OrbitArchive? _imported;

    [Fact]
    public async Task A_sealed_place_is_written_with_its_opened_content()
    {
        var client = ClientThatOpens(new SealedPlace(
            "The spare key", "Under the third pot", new EventLocationDto("Piękna 1, Warszawa", 52.2297, 21.0122)));

        var opened = await client.OpenPlacesAsync(ArchiveOf(SealedAsTheServerHoldsIt));

        var place = Assert.Single(opened.Archive.AllPlaces);
        Assert.Equal("The spare key", place.Name);
        Assert.Equal("Under the third pot", place.Description);
        Assert.Equal("Piękna 1, Warszawa", place.Where.Address);
        Assert.Equal(52.2297, place.Where.Latitude);
        Assert.Equal(21.0122, place.Where.Longitude);
        // What the server could already read comes through unchanged.
        Assert.Equal("#3366ff", place.Colour);
        Assert.Equal(["Viewing"], place.TaskListTitles);
        // The sealed half stays beside the words, so the file still imports sealed.
        Assert.True(place.IsPrivate);
        Assert.Equal("c2VhbGVk", place.EncryptedContent!.Ciphertext);
        Assert.Equal(0, opened.UnopenedPlaces);
    }

    /// <summary>
    /// One this browser cannot open - sealed under a key since replaced - is written as the server gave
    /// it and counted, rather than failing the whole export over one row.
    /// </summary>
    [Fact]
    public async Task A_place_this_browser_cannot_open_is_kept_empty_and_counted()
    {
        var client = ClientThatOpens(plainText: null);

        var opened = await client.OpenPlacesAsync(ArchiveOf(SealedAsTheServerHoldsIt));

        Assert.Equal(string.Empty, Assert.Single(opened.Archive.AllPlaces).Name);
        Assert.Equal(1, opened.UnopenedPlaces);
    }

    /// <summary>
    /// No places, no key: an export somebody left places out of must not fail on a browser holding no
    /// key. "decryptForSelf" is not planned here, so asking for it would throw.
    /// </summary>
    [Fact]
    public async Task An_archive_without_places_never_asks_for_the_key()
    {
        var client = ClientThatCannotOpen();
        var archive = ArchiveOf() with { Places = [] };

        var opened = await client.OpenPlacesAsync(archive);

        Assert.Empty(opened.Archive.AllPlaces);
        Assert.Empty(JSInterop.Invocations["decryptForSelf"]);
    }

    [Fact]
    public async Task An_open_place_is_written_as_it_came()
    {
        var client = ClientThatCannotOpen();
        var open = new ArchivedPlace(
            "The good bakery", "", new ArchivedEventLocation("Rynek 2", 50.06, 19.94), "", "Normal", [],
            IsPrivate: false, EncryptedContent: null);

        var opened = await client.OpenPlacesAsync(ArchiveOf(open));

        Assert.Equal(open, Assert.Single(opened.Archive.AllPlaces));
    }

    /// <summary>
    /// A file of opened places is what the reader asked for; the server reading one is not. The words are
    /// emptied before the file goes up, and the sealed half is what the server restores it from.
    /// </summary>
    [Fact]
    public async Task Importing_sends_a_private_place_up_closed()
    {
        var client = ClientThatCannotOpen();
        var openedByTheBrowser = SealedAsTheServerHoldsIt with
        {
            Name = "The spare key",
            Description = "Under the third pot",
            Where = new ArchivedEventLocation("Piękna 1, Warszawa", 52.2297, 21.0122)
        };

        var result = await client.ImportAsync(ArchiveOf(openedByTheBrowser));

        var sent = Assert.Single(_imported!.AllPlaces);
        Assert.Equal(string.Empty, sent.Name);
        Assert.Equal(string.Empty, sent.Description);
        Assert.Equal(string.Empty, sent.Where.Address);
        Assert.Equal(0, sent.Where.Latitude);
        Assert.Equal("c2VhbGVk", sent.EncryptedContent!.Ciphertext);
        Assert.Equal(1, result!.Places);
    }

    private static OrbitArchive ArchiveOf(params ArchivedPlace[] places)
        => new(OrbitArchive.CurrentVersion, DateTimeOffset.UtcNow, [], [], [], [], places);

    /// <summary>A client whose "decryptForSelf" answers with this place, or with null for one it cannot open.</summary>
    private TransferApiClient ClientThatOpens(SealedPlace? plainText)
    {
        JSInterop.SetupModule("./js/e2eeChat.js")
            .Setup<string?>("decryptForSelf", _ => true)
            .SetResult(plainText is null ? null : JsonSerializer.Serialize(plainText));
        return Client();
    }

    /// <summary>A client with nothing planned for the key at all - any attempt to open something fails.</summary>
    private TransferApiClient ClientThatCannotOpen()
    {
        JSInterop.SetupModule("./js/e2eeChat.js");
        return Client();
    }

    private TransferApiClient Client()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        tokenStore.SetTokenAsync(AnUnsignedTokenFor(OwnUserId)).GetAwaiter().GetResult();
        var refreshClient = new HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var authenticationStateProvider = new OrbitAuthenticationStateProvider(
            tokenStore, new TokenRefreshService(tokenStore, refreshClient));

        var httpClient = new HttpClient(new StubHttpMessageHandler(Answer))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var sealer = new PrivateContentSealer(
            new OwnEncryptionKeyProvider(JSInterop.JSRuntime, new UsersApiClient(httpClient), authenticationStateProvider),
            authenticationStateProvider,
            JSInterop.JSRuntime);

        return new TransferApiClient(httpClient, sealer);
    }

    /// <summary>
    /// Stands in for the import endpoint, and counts places the way ImportArchiveCommandHandler does: a
    /// private one with no sealed half is left out, so this answers no more generously than the server.
    /// </summary>
    private HttpResponseMessage Answer(HttpRequestMessage request)
    {
        _imported = request.Content!.ReadFromJsonAsync<OrbitArchive>().GetAwaiter().GetResult();
        var places = _imported!.AllPlaces.Count(place => !place.IsPrivate || place.EncryptedContent is not null);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new ImportArchiveResult(
                _imported.Notes.Count, _imported.TaskLists.Count, _imported.CalendarEvents.Count,
                _imported.Inventories.Count, places))
        };
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
