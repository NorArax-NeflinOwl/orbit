using System.Net;
using System.Net.Http.Json;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Sharing;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Orbit.Web.Tests;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// Covers the page behind a public link - the one page in Orbit a reader with no account is meant to
/// reach. What matters is that it shows the item, offers no way to change it, and offers signing in
/// only as a way to keep a copy.
/// </summary>
public sealed class SharedItemPageTests : OrbitTestContext
{
    private readonly TestAuthorizationContext _authorization;

    public SharedItemPageTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Needed even for the signed-out cases: the page's AuthorizeView asks for the cascading
        // authentication state either way, and without one it throws rather than rendering as anonymous.
        _authorization = this.AddTestAuthorization();
    }

    [Fact]
    public void A_shared_item_shows_its_title_lines_and_who_shared_it()
    {
        RegisterApiClient(Item("Shopping list", Line("Milk"), Line("Bread")));

        var cut = RenderComponent<SharedItemPage>(parameters => parameters.Add(page => page.Token, "a-token"));

        Assert.Contains("Shopping list", cut.Markup);
        Assert.Contains("Milk", cut.Markup);
        Assert.Contains("Shared by Anna Kowalska", cut.Markup);
    }

    [Fact]
    public void A_link_that_resolves_to_nothing_says_so_without_guessing_why()
    {
        // Revoked, unknown, deleted and since-made-private all arrive here identically on purpose - see
        // GetPublicSharedItemQueryHandler.
        RegisterApiClient(item: null);

        var cut = RenderComponent<SharedItemPage>(parameters => parameters.Add(page => page.Token, "a-token"));

        Assert.Contains("This link doesn't work", cut.Markup);
    }

    [Fact]
    public void A_checklist_line_is_shown_but_cannot_be_ticked()
    {
        RegisterApiClient(Item("Groceries", Line("Milk", isChecklistItem: true, isChecked: true)));

        var cut = RenderComponent<SharedItemPage>(parameters => parameters.Add(page => page.Token, "a-token"));

        // The reader can see what is done without being offered a control that would do nothing.
        Assert.True(cut.Find(".tick-box").HasAttribute("disabled"));
    }

    [Fact]
    public void A_reader_with_no_account_is_offered_signing_in_rather_than_saving()
    {
        RegisterApiClient(Item("Shopping list", Line("Milk")));

        var cut = RenderComponent<SharedItemPage>(parameters => parameters.Add(page => page.Token, "a-token"));

        Assert.Contains("Sign in to save this", cut.Markup);
        Assert.DoesNotContain("Save to my account", cut.Markup);
    }

    [Fact]
    public void A_signed_in_reader_is_offered_saving_it()
    {
        _authorization.SetAuthorized("anna");
        RegisterApiClient(Item("Shopping list", Line("Milk")));

        var cut = RenderComponent<SharedItemPage>(parameters => parameters.Add(page => page.Token, "a-token"));

        Assert.Contains("Save to my account", cut.Markup);
    }

    [Fact]
    public void Saving_it_says_what_happened()
    {
        _authorization.SetAuthorized("anna");
        RegisterApiClient(Item("Shopping list", Line("Milk")));
        var cut = RenderComponent<SharedItemPage>(parameters => parameters.Add(page => page.Token, "a-token"));

        cut.FindAll("button").First(button => button.TextContent.Contains("Save to my account")).Click();

        Assert.Contains("Saved", cut.Markup);
    }

    private void RegisterApiClient(PublicSharedItemDto? item)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new ClaimPublicShareLinkResponse("Note", Guid.NewGuid(), AlreadyHeld: false))
                };
            }

            return item is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(item) };
        });

        Services.AddSingleton(new PublicShareApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));
    }

    /// <summary>
    /// A link can point at a whole folder since 2026-09-20: one address showing everything filed under
    /// a tab, each thing drawn as its own link would draw it. The user asked for it.
    /// </summary>
    [Fact]
    public void A_link_to_a_folder_shows_every_thing_in_it()
    {
        RegisterApiClient(Folder(
            "Recipes",
            Item("Pierogi", Line("Flour"), Line("Potato")),
            Item("Bigos", Line("Cabbage"))));

        var cut = RenderComponent<SharedItemPage>(parameters => parameters.Add(page => page.Token, "a-token"));

        Assert.Contains("Recipes", cut.Markup);
        Assert.Contains("Pierogi", cut.Markup);
        Assert.Contains("Flour", cut.Markup);
        Assert.Contains("Bigos", cut.Markup);
        // Whose it is is said once, above them, rather than on every card.
        Assert.Single(cut.FindAll(".shared-item-owner"));
    }

    /// <summary>
    /// And no offer to keep it. There is no such thing as a share of a folder - it stays the owner's own
    /// tab - and the button's promise is one read-only copy, where a folder would hand over a page of
    /// them. The page points at the other half of sharing a folder instead.
    /// </summary>
    [Fact]
    public void A_folder_offers_no_way_to_keep_it_and_says_what_to_do_instead()
    {
        _authorization.SetAuthorized("anna");
        RegisterApiClient(Folder("Recipes", Item("Pierogi")));

        var cut = RenderComponent<SharedItemPage>(parameters => parameters.Add(page => page.Token, "a-token"));

        Assert.DoesNotContain("Save to my account", cut.Markup);
        Assert.Contains("ask them to share the folder with you", cut.Markup);
    }

    private static PublicSharedItemDto Item(string title, params PublicSharedItemLineDto[] lines)
        => new("Note", title, Subtitle: null, lines, "Anna Kowalska", DateTimeOffset.UtcNow);

    /// <summary>A folder as a link sends it: no lines of its own, and everything under it in Items.</summary>
    private static PublicSharedItemDto Folder(string name, params PublicSharedItemDto[] inside)
        => new(
            "Folder", name, $"{inside.Length} items", [], "Anna Kowalska", DateTimeOffset.UtcNow, inside);

    private static PublicSharedItemLineDto Line(string text, bool isChecklistItem = false, bool isChecked = false)
        => new(text, isChecklistItem, isChecked, Detail: null);
}
