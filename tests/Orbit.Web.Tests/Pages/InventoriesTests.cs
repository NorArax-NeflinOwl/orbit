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
using Orbit.Contracts.Inventories;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// The list of inventories: one card each, saying what this reader may do with it. Which buttons appear
/// is the whole point - every one of them is offered only when the server would accept the click, so
/// nothing here leads to a refusal.
/// </summary>
public sealed class InventoriesTests : OrbitTestContext
{
    private static readonly Guid OwnUserId = Guid.NewGuid();

    public InventoriesTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        RegisterAuthentication();
    }

    [Fact]
    public void Each_inventory_gets_a_card()
    {
        RegisterApiClients([Inventory("Pantry"), Inventory("Garage")]);

        var cut = RenderComponent<Web.Pages.Inventories>();

        Assert.Equal(2, cut.FindAll(".item-card").Count);
        Assert.Contains("Pantry", cut.Markup);
        Assert.Contains("Garage", cut.Markup);
    }

    [Fact]
    public void An_account_with_no_inventories_is_told_what_to_do_about_it()
    {
        RegisterApiClients([]);

        var cut = RenderComponent<Web.Pages.Inventories>();

        Assert.Contains("No inventories yet", cut.Markup);
    }

    [Fact]
    public void A_inventory_that_will_not_load_says_so_rather_than_showing_an_empty_shelf()
    {
        // An empty list and a failed request look the same on screen, and one of them is a lie.
        RegisterApiClients(inventories: null);

        var cut = RenderComponent<Web.Pages.Inventories>();

        Assert.Contains("Couldn't load your inventories", cut.Markup);
    }

    [Fact]
    public void A_private_inventory_says_what_that_means_and_cannot_be_shared()
    {
        // The server refuses to share one, so offering the button would only lead to a refusal.
        RegisterApiClients([Inventory("Medicine", isPrivate: true)]);

        var cut = RenderComponent<Web.Pages.Inventories>();

        Assert.Contains("only you can read it", cut.Markup);
        Assert.DoesNotContain("Share", ActionsOf(cut));
    }

    [Fact]
    public void A_inventory_shared_with_this_reader_says_who_by_and_on_what_terms()
    {
        RegisterApiClients([Inventory("Pantry", isShared: true, sharedByUserName: "Anna", accessLevel: "ReadOnly")]);

        var cut = RenderComponent<Web.Pages.Inventories>();

        Assert.Contains("Shared by Anna", cut.Markup);
    }

    [Fact]
    public void Somebody_elses_inventory_is_not_offered_for_deleting()
    {
        // Deleting it would take it from its owner, which is not what "remove this from my list" means.
        RegisterApiClients([Inventory("Pantry", isShared: true, sharedByUserName: "Anna", accessLevel: "CanEdit")]);

        var cut = RenderComponent<Web.Pages.Inventories>();

        Assert.DoesNotContain("Delete", ActionsOf(cut));
    }

    [Fact]
    public void A_read_only_share_cannot_be_passed_on_further()
    {
        // A re-share can never grant more than the re-sharer holds, and ReadOnly holds no right to share.
        RegisterApiClients([Inventory("Pantry", isShared: true, sharedByUserName: "Anna", accessLevel: "ReadOnly")]);

        var cut = RenderComponent<Web.Pages.Inventories>();

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
        RegisterApiClients([Inventory("Pantry", isShared: true, sharedByUserName: "Anna", accessLevel: "ReadOnly")]);

        var cut = RenderComponent<Web.Pages.Inventories>();
        var actions = ActionsOf(cut);

        Assert.Contains("View", actions);
        Assert.DoesNotContain("Edit", actions);
    }

    /// <summary>A share granted CanEdit still says "Edit": there is something to save, and the card
    /// promises exactly that.</summary>
    [Fact]
    public void A_share_granted_CanEdit_still_offers_Edit()
    {
        RegisterApiClients([Inventory("Pantry", isShared: true, sharedByUserName: "Anna", accessLevel: "CanEdit")]);

        var cut = RenderComponent<Web.Pages.Inventories>();

        Assert.Contains("Edit", ActionsOf(cut));
    }

    [Fact]
    public void A_inventory_somebody_else_is_editing_says_so()
    {
        RegisterApiClients([Inventory("Pantry", lockedByUserName: "Anna")]);

        var cut = RenderComponent<Web.Pages.Inventories>();

        Assert.Contains("Anna is editing it right now", cut.Markup);
    }

    [Fact]
    public void Opening_one_goes_to_its_shelf()
    {
        var pantry = Inventory("Pantry");
        RegisterApiClients([pantry]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<Web.Pages.Inventories>();

        cut.Find(".item-card-name").Click();

        Assert.EndsWith($"/inventory/{pantry.Id}", navigationManager.Uri);
    }

    /// <summary>
    /// Making one is a named address now, the same as every other object - see InventoryEditor's
    /// "/inventory/new" route. An inventory used to be made in a box right here instead.
    /// </summary>
    [Fact]
    public void Adding_one_opens_its_own_form()
    {
        RegisterApiClients([]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<Web.Pages.Inventories>();

        cut.Find(".page-add").Click();

        Assert.EndsWith("/inventory/new", navigationManager.Uri);
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

    private static InventoryDto Inventory(
        string name, bool isPrivate = false, bool isShared = false, string? sharedByUserName = null,
        string accessLevel = "CanEdit", string? lockedByUserName = null)
        => new(
            Guid.NewGuid(), name, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            isShared, sharedByUserName, accessLevel, lockedByUserName,
            OriginalOwnerUserId: isShared ? Guid.NewGuid() : null, isPrivate);

    /// <param name="inventories">Null stands for a request that never came back.</param>
    private void RegisterApiClients(IReadOnlyList<InventoryDto>? inventories)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/inventories", StringComparison.Ordinal))
            {
                return inventories is null
                    ? throw new HttpRequestException("The API could not be reached.")
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(inventories) };
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
