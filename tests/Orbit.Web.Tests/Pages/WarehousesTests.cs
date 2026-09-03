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
/// The list of warehouses: one card each, saying what this reader may do with it. Which buttons appear
/// is the whole point - every one of them is offered only when the server would accept the click, so
/// nothing here leads to a refusal.
/// </summary>
public sealed class WarehousesTests : OrbitTestContext
{
    private static readonly Guid OwnUserId = Guid.NewGuid();

    public WarehousesTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        RegisterAuthentication();
    }

    [Fact]
    public void Each_warehouse_gets_a_card()
    {
        RegisterApiClients([Warehouse("Pantry"), Warehouse("Garage")]);

        var cut = RenderComponent<Web.Pages.Warehouses>();

        Assert.Equal(2, cut.FindAll(".item-card").Count);
        Assert.Contains("Pantry", cut.Markup);
        Assert.Contains("Garage", cut.Markup);
    }

    [Fact]
    public void An_account_with_no_warehouses_is_told_what_to_do_about_it()
    {
        RegisterApiClients([]);

        var cut = RenderComponent<Web.Pages.Warehouses>();

        Assert.Contains("No warehouses yet", cut.Markup);
    }

    [Fact]
    public void A_warehouse_that_will_not_load_says_so_rather_than_showing_an_empty_shelf()
    {
        // An empty list and a failed request look the same on screen, and one of them is a lie.
        RegisterApiClients(warehouses: null);

        var cut = RenderComponent<Web.Pages.Warehouses>();

        Assert.Contains("Couldn't load your warehouses", cut.Markup);
    }

    [Fact]
    public void A_private_warehouse_says_what_that_means_and_cannot_be_shared()
    {
        // The server refuses to share one, so offering the button would only lead to a refusal.
        RegisterApiClients([Warehouse("Medicine", isPrivate: true)]);

        var cut = RenderComponent<Web.Pages.Warehouses>();

        Assert.Contains("only you can read it", cut.Markup);
        Assert.DoesNotContain("Share", ActionsOf(cut));
    }

    [Fact]
    public void A_warehouse_shared_with_this_reader_says_who_by_and_on_what_terms()
    {
        RegisterApiClients([Warehouse("Pantry", isShared: true, sharedByUserName: "Anna", accessLevel: "ReadOnly")]);

        var cut = RenderComponent<Web.Pages.Warehouses>();

        Assert.Contains("Shared by Anna", cut.Markup);
    }

    [Fact]
    public void Somebody_elses_warehouse_is_not_offered_for_deleting()
    {
        // Deleting it would take it from its owner, which is not what "remove this from my list" means.
        RegisterApiClients([Warehouse("Pantry", isShared: true, sharedByUserName: "Anna", accessLevel: "CanEdit")]);

        var cut = RenderComponent<Web.Pages.Warehouses>();

        Assert.DoesNotContain("Delete", ActionsOf(cut));
    }

    [Fact]
    public void A_read_only_share_cannot_be_passed_on_further()
    {
        // A re-share can never grant more than the re-sharer holds, and ReadOnly holds no right to share.
        RegisterApiClients([Warehouse("Pantry", isShared: true, sharedByUserName: "Anna", accessLevel: "ReadOnly")]);

        var cut = RenderComponent<Web.Pages.Warehouses>();

        Assert.DoesNotContain("Share", ActionsOf(cut));
    }

    /// <summary>
    /// A share below CanEdit still opens the shelf to be looked at, but nothing there can be saved, so
    /// the card's menu says "View" rather than promising an Edit that will refuse - the same rule
    /// Notes.razor's own card already follows.
    /// </summary>
    [Fact]
    public void A_read_only_share_offers_View_instead_of_Edit()
    {
        RegisterApiClients([Warehouse("Pantry", isShared: true, sharedByUserName: "Anna", accessLevel: "ReadOnly")]);

        var cut = RenderComponent<Web.Pages.Warehouses>();
        var actions = ActionsOf(cut);

        Assert.Contains("View", actions);
        Assert.DoesNotContain("Edit", actions);
    }

    /// <summary>A share granted CanEdit still says "Edit": there is something to save, and the card
    /// promises exactly that.</summary>
    [Fact]
    public void A_share_granted_CanEdit_still_offers_Edit()
    {
        RegisterApiClients([Warehouse("Pantry", isShared: true, sharedByUserName: "Anna", accessLevel: "CanEdit")]);

        var cut = RenderComponent<Web.Pages.Warehouses>();

        Assert.Contains("Edit", ActionsOf(cut));
    }

    [Fact]
    public void A_warehouse_somebody_else_is_editing_says_so()
    {
        RegisterApiClients([Warehouse("Pantry", lockedByUserName: "Anna")]);

        var cut = RenderComponent<Web.Pages.Warehouses>();

        Assert.Contains("Anna is editing it right now", cut.Markup);
    }

    [Fact]
    public void Opening_one_goes_to_its_shelf()
    {
        var pantry = Warehouse("Pantry");
        RegisterApiClients([pantry]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<Web.Pages.Warehouses>();

        cut.Find(".item-card-name").Click();

        Assert.EndsWith($"/inventory/{pantry.Id}", navigationManager.Uri);
    }

    [Fact]
    public void Creating_one_is_offered_but_not_until_it_has_a_name()
    {
        RegisterApiClients([]);
        var cut = RenderComponent<Web.Pages.Warehouses>();

        cut.Find(".page-add").Click();

        var create = cut.FindAll(".warehouse-create-row button").First(button => button.TextContent.Trim() == "Create");
        Assert.True(create.HasAttribute("disabled"));
    }

    [Fact]
    public void A_named_warehouse_can_be_created()
    {
        RegisterApiClients([]);
        var cut = RenderComponent<Web.Pages.Warehouses>();
        cut.Find(".page-add").Click();

        cut.Find("#newWarehouseNameInput").Input("Cellar");

        var create = cut.FindAll(".warehouse-create-row button").First(button => button.TextContent.Trim() == "Create");
        Assert.False(create.HasAttribute("disabled"));
    }

    /// <summary>
    /// What a card offers, which is now what its overflow menu holds - see ItemCard's Menu slot. The
    /// menu renders its entries only once opened, so this opens the first card's before reading it.
    /// </summary>
    private static string ActionsOf(IRenderedFragment cut)
    {
        if (cut.FindAll(".item-card .overflow-menu-trigger").FirstOrDefault() is not { } trigger)
        {
            // A card with nothing to offer renders no menu at all, which is an empty list of actions.
            return string.Empty;
        }

        trigger.Click();
        return cut.Find(".item-card-menu").TextContent;
    }

    private static WarehouseDto Warehouse(
        string name, bool isPrivate = false, bool isShared = false, string? sharedByUserName = null,
        string accessLevel = "CanEdit", string? lockedByUserName = null)
        => new(
            Guid.NewGuid(), name, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            isShared, sharedByUserName, accessLevel, lockedByUserName,
            OriginalOwnerUserId: isShared ? Guid.NewGuid() : null, isPrivate);

    /// <param name="warehouses">Null stands for a request that never came back.</param>
    private void RegisterApiClients(IReadOnlyList<WarehouseDto>? warehouses)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/warehouses", StringComparison.Ordinal))
            {
                return warehouses is null
                    ? throw new HttpRequestException("The API could not be reached.")
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(warehouses) };
            }

            // Contacts, for the share picker - nobody to share with is the right default here.
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) };
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new InventoryApiClient(httpClient));
        Services.AddSingleton(new ChatApiClient(httpClient));
    }

    /// <summary>
    /// The page reads the signed-in user's id before it loads anything, and injects the encrypted
    /// sender for the share flow. Neither is what these tests are about; both have to resolve.
    /// </summary>
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
