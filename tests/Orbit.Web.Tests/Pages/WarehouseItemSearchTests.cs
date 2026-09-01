using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Inventory;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// "Which warehouse is the flour in?" - the question the inventory page could not answer, since it
/// lists shelves and not what is on them. What matters is that the answer is complete: a search that
/// quietly leaves a warehouse out says "nowhere" when the truth is "I could not look there".
/// </summary>
public sealed class WarehouseItemSearchTests : OrbitTestContext
{
    private static readonly Guid OwnUserId = Guid.NewGuid();
    private static readonly Guid PantryId = Guid.NewGuid();
    private static readonly Guid CellarId = Guid.NewGuid();

    /// <summary>Warehouses whose items answer with a failure, for the "could not look there" case.</summary>
    private readonly HashSet<Guid> _unreadable = [];

    public WarehouseItemSearchTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        RegisterAuthentication();
    }

    [Fact]
    public void Nothing_is_searched_until_something_is_asked_for()
    {
        // Opening this page to look at the shelves should cost nothing extra.
        RegisterApiClients(Shelves());

        var cut = Render();

        Assert.Single(cut.FindAll(".warehouse-search input"));
        Assert.Empty(cut.FindAll(".warehouse-search-results"));
    }

    [Fact]
    public void An_item_is_found_together_with_the_warehouse_it_is_in()
    {
        RegisterApiClients(Shelves());
        var cut = Render();

        SearchFor(cut, "flour");

        var row = Assert.Single(cut.FindAll(".warehouse-search-results .list-row"));
        Assert.Contains("Wheat flour", row.TextContent);
        Assert.Contains("Pantry", row.TextContent);
    }

    [Fact]
    public void The_same_thing_on_two_shelves_is_found_on_both()
    {
        // Which is the whole point: the reader does not know where it is, and it may be in two places.
        RegisterApiClients(Shelves());
        var cut = Render();

        SearchFor(cut, "salt");

        var rows = cut.FindAll(".warehouse-search-results .list-row").Select(row => row.TextContent).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.Contains("Pantry", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Contains("Cellar", StringComparison.Ordinal));
        Assert.Contains("Found in 2 of 2 warehouses.", cut.Markup);
    }

    [Fact]
    public void A_match_is_found_anywhere_in_the_name_and_whatever_the_case()
    {
        RegisterApiClients(Shelves());
        var cut = Render();

        SearchFor(cut, "FLO");

        Assert.Single(cut.FindAll(".warehouse-search-results .list-row"));
    }

    [Fact]
    public void How_much_there_is_comes_with_it()
    {
        // Half the answer to "where is it" is "and is there any left".
        RegisterApiClients(Shelves());
        var cut = Render();

        SearchFor(cut, "flour");

        Assert.Contains("2 kg", cut.Find(".warehouse-search-results .list-row").TextContent);
    }

    [Fact]
    public void Something_counted_one_by_one_is_written_as_a_bare_number()
    {
        // "2" of a thing already means two of them - the rule a restock errand is written by too.
        RegisterApiClients(Shelves());
        var cut = Render();

        SearchFor(cut, "candle");

        var row = Assert.Single(cut.FindAll(".warehouse-search-results .list-row"));
        Assert.Contains("6", row.TextContent);
        Assert.DoesNotContain("pcs", row.TextContent);
    }

    [Fact]
    public void A_result_opens_the_warehouse_it_was_found_in()
    {
        RegisterApiClients(Shelves());
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = Render();
        SearchFor(cut, "flour");

        cut.Find(".warehouse-search-results .list-row").Click();

        Assert.EndsWith($"/inventory/{PantryId}", navigationManager.Uri);
    }

    [Fact]
    public void Nothing_anywhere_says_so_rather_than_showing_an_empty_list()
    {
        RegisterApiClients(Shelves());
        var cut = Render();

        SearchFor(cut, "there is no such thing");

        Assert.Contains("Nothing on any shelf matches that.", cut.Markup);
    }

    [Fact]
    public void A_warehouse_that_could_not_be_opened_is_named_rather_than_skipped()
    {
        // The failure that matters: a shelf left out of the search turns "I could not look there" into
        // "it is nowhere", and the reader has no way to tell.
        _unreadable.Add(CellarId);
        RegisterApiClients(Shelves());
        var cut = Render();

        SearchFor(cut, "flour");

        Assert.Contains("could not be opened", cut.Markup);
        Assert.Contains("Cellar", cut.Markup);
    }

    [Fact]
    public void Clearing_the_search_puts_the_shelves_back()
    {
        RegisterApiClients(Shelves());
        var cut = Render();
        SearchFor(cut, "flour");

        cut.FindAll(".warehouse-search button").First(button => button.TextContent.Contains("Clear")).Click();

        Assert.Empty(cut.FindAll(".warehouse-search-results"));
        Assert.Equal(2, cut.FindAll(".item-card").Count);
    }

    [Fact]
    public void An_account_with_no_warehouses_is_offered_no_search()
    {
        RegisterApiClients([]);

        var cut = Render();

        Assert.Empty(cut.FindAll(".warehouse-search"));
    }

    private IRenderedComponent<Web.Pages.Warehouses> Render() => RenderComponent<Web.Pages.Warehouses>();

    private static void SearchFor(IRenderedComponent<Web.Pages.Warehouses> cut, string text)
        => cut.Find(".warehouse-search input").Input(text);

    /// <summary>Two warehouses, with salt on both - which is the case the search exists for.</summary>
    private static Dictionary<Guid, (string Name, InventoryItemDto[] Items)> Shelves()
        => new()
        {
            [PantryId] = ("Pantry", [Item("Wheat flour", 2, "Kilogram"), Item("Salt", 1, "Kilogram")]),
            [CellarId] = ("Cellar", [Item("Salt", 3, "Kilogram"), Item("Candles", 6, "Piece")])
        };

    private static InventoryItemDto Item(string name, decimal quantity, string unit)
        => new(
            Guid.NewGuid(), name, "Food", "Dry", quantity, MinimumQuantity: null, unit,
            ExpiryDate: null, ExpiryNotificationChannel: "None", IsBelowMinimum: false,
            HasPendingRestockTask: false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private void RegisterApiClients(Dictionary<Guid, (string Name, InventoryItemDto[] Items)> shelves)
    {
        var warehouses = shelves
            .Select(shelf => new WarehouseDto(
                shelf.Key, shelf.Value.Name, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit",
                LockedByUserName: null, OriginalOwnerUserId: null))
            .ToList();

        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            // A warehouse's contents, which is what the search reads. Asked for one warehouse at a
            // time, because a private one can only be opened here - see the page's own comment.
            if (path.EndsWith("/items", StringComparison.Ordinal))
            {
                var warehouseId = WarehouseIdIn(path);
                return _unreadable.Contains(warehouseId)
                    ? throw new HttpRequestException("That warehouse could not be read.")
                    : Ok(shelves[warehouseId].Items);
            }

            // The single warehouse the item read checks for privacy before opening anything.
            if (WarehouseIdIn(path) is var id && shelves.ContainsKey(id))
            {
                return Ok(warehouses.First(warehouse => warehouse.Id == id));
            }

            return path.Contains("/warehouses", StringComparison.Ordinal)
                ? Ok(warehouses)
                : Ok(Array.Empty<object>());
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new InventoryApiClient(httpClient));
        Services.AddSingleton(new ChatApiClient(httpClient));
    }

    /// <summary>The warehouse a path names, or empty where it names none - "/api/warehouses" itself.</summary>
    private static Guid WarehouseIdIn(string path)
        => path.Split('/').Select(segment => Guid.TryParse(segment, out var id) ? id : Guid.Empty)
            .FirstOrDefault(id => id != Guid.Empty);

    private static HttpResponseMessage Ok<T>(T body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private void RegisterAuthentication()
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        tokenStore.SetTokenAsync(CreateUnsignedJwt(new Dictionary<string, string>
        {
            ["sub"] = OwnUserId.ToString(),
            ["email"] = "owner@example.com",
            ["name"] = "Test Owner"
        })).GetAwaiter().GetResult();
        var refreshHttpClient = new HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var authenticationStateProvider = new OrbitAuthenticationStateProvider(
            tokenStore, new TokenRefreshService(tokenStore, refreshHttpClient));
        Services.AddSingleton(authenticationStateProvider);
        Services.AddSingleton<AuthenticationStateProvider>(authenticationStateProvider);
        Services.AddAuthorizationCore();

        var jsRuntime = JSInterop.JSRuntime;
        var usersApiClient = new UsersApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") });
        Services.AddSingleton(usersApiClient);
        Services.AddSingleton(new EncryptedChatMessageSender(
            jsRuntime,
            new OwnEncryptionKeyProvider(jsRuntime, usersApiClient, authenticationStateProvider),
            usersApiClient,
            new ChatApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") })));
    }

    private static string CreateUnsignedJwt(Dictionary<string, string> claims)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(claims));
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
