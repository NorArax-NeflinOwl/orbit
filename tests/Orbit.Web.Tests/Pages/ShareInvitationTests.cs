using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Sharing;
using Orbit.Contracts.Users;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// Where a notification about something shared leads. It used to lead to the conversation with whoever
/// sent it, because that is where Accept lived - on an encrypted message only that browser's key can
/// open. The offer itself is the server's, so this page can show it and take it up wherever the reader
/// is, and the notification can point at what was shared rather than at a chat.
/// </summary>
public sealed class ShareInvitationTests : OrbitTestContext
{
    private static readonly Guid ShareId = Guid.NewGuid();
    private static readonly Guid SharerId = Guid.NewGuid();

    /// <summary>Null stands for an offer the server says nothing about - withdrawn, or never this reader's.</summary>
    private bool? _isAccepted;
    private string _itemTitle = "Shopping";
    private static readonly Guid ItemId = Guid.NewGuid();
    private readonly List<string> _postedTo = [];

    public ShareInvitationTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        RegisterApiClients();
    }

    [Fact]
    public void An_open_offer_says_who_sent_it_and_names_the_thing()
    {
        _isAccepted = false;

        var cut = Render("note");

        Assert.Contains("Anna Kowalska", cut.Markup);
        Assert.Contains("Shopping", cut.Markup);
        Assert.Contains(cut.FindAll("button"), button => button.TextContent.Contains("Accept"));
    }

    /// <summary>
    /// Something deleted between the offer and the reading of it comes back with no name and the offer
    /// still standing - so the sentence says what kind of thing it was rather than leaving a gap.
    /// </summary>
    [Fact]
    public void An_offer_whose_thing_is_gone_still_says_what_kind_it_was()
    {
        _isAccepted = false;
        _itemTitle = string.Empty;

        var cut = Render("note");

        Assert.Contains("a note", cut.Markup);
        Assert.Contains(cut.FindAll("button"), button => button.TextContent.Contains("Accept"));
    }

    /// <summary>
    /// The whole point of the page: the same accept the conversation offers, reachable from the
    /// notification - and afterwards the reader is put where the thing now is.
    /// </summary>
    [Fact]
    public void Accepting_takes_it_up_and_goes_to_where_it_now_is()
    {
        _isAccepted = false;
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = Render("tasklist");

        cut.FindAll("button").First(button => button.TextContent.Contains("Accept")).Click();

        Assert.Contains(_postedTo, path => path == $"/api/tasks/shares/{ShareId}/accept");
        // The thing itself, not the list it is now in: the reader pressed a notification about one
        // particular thing, and a page of everything makes them look for it again.
        Assert.EndsWith($"/tasks/{ItemId}", navigationManager.Uri);
    }

    /// <summary>Each kind is taken up at its own endpoint - the ones the conversation's Accept already uses.</summary>
    [Theory]
    [InlineData("note", "/api/notes/shares/")]
    [InlineData("tasklist", "/api/tasks/shares/")]
    [InlineData("event", "/api/calendar-events/shares/")]
    [InlineData("inventory", "/api/inventories/shares/")]
    [InlineData("place", "/api/places/shares/")]
    public void Every_kind_is_accepted_where_that_kind_is_accepted(string kind, string expectedPath)
    {
        _isAccepted = false;
        var cut = Render(kind);

        cut.FindAll("button").First(button => button.TextContent.Contains("Accept")).Click();

        Assert.Contains(_postedTo, path => path.StartsWith(expectedPath, StringComparison.Ordinal));
    }

    /// <summary>
    /// A place is met on the map, which has no page of a place's own - so accepting one lands on the map
    /// opened at that pin rather than on a path that does not exist. See MapPage.Place.
    /// </summary>
    [Fact]
    public void Accepting_a_place_lands_on_the_map_at_that_pin()
    {
        _isAccepted = false;
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = Render("place");

        cut.FindAll("button").First(button => button.TextContent.Contains("Accept")).Click();

        Assert.EndsWith($"/map?place={ItemId}", navigationManager.Uri);
    }

    /// <summary>
    /// An offer already taken up is not an error and not a second Accept: the notification is often read
    /// long after the offer was answered in the conversation.
    /// </summary>
    [Fact]
    public void An_offer_already_accepted_says_so_and_offers_the_way_in()
    {
        _isAccepted = true;

        var cut = Render("note");

        Assert.Contains("yours already", cut.Markup);
        Assert.DoesNotContain(cut.FindAll("button"), button => button.TextContent.Contains("Accept"));
        Assert.Equal($"/notes/{ItemId}", cut.Find("a.btn-primary").GetAttribute("href"));
    }

    /// <summary>
    /// Withdrawn, or never this reader's - the server answers the same way for both on purpose, and so
    /// does this page: telling them apart would say whether a share id exists.
    /// </summary>
    [Fact]
    public void An_offer_that_is_no_longer_there_says_so()
    {
        _isAccepted = null;

        var cut = Render("note");

        Assert.Contains("no longer there", cut.Markup);
        Assert.DoesNotContain(cut.FindAll("button"), button => button.TextContent.Contains("Accept"));
    }

    /// <summary>An older client against a newer server: it says so rather than drawing an empty card.</summary>
    [Fact]
    public void A_kind_this_build_does_not_know_says_so()
    {
        _isAccepted = false;

        var cut = Render("something-new");

        Assert.Contains("doesn't know about", cut.Markup);
    }

    private IRenderedComponent<ShareInvitation> Render(string kind)
        => RenderComponent<ShareInvitation>(parameters => parameters
            .Add(page => page.Kind, kind)
            .Add(page => page.ShareId, ShareId)
            .Add(page => page.SharedByUserId, SharerId));

    private void RegisterApiClients()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Post)
            {
                _postedTo.Add(path);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path.StartsWith("/api/users/", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(
                        new UserSearchResultDto(SharerId, "anna", "Anna Kowalska", "a-public-key"))
                };
            }

            // The offer itself: what was offered, what it is called, and whether it has been taken up -
            // or nothing at all for an offer that is not this reader's.
            return _isAccepted is { } isAccepted
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new ShareOfferDto(ItemId, _itemTitle, isAccepted))
                }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        Services.AddSingleton(new SharesApiClient(httpClient));
        Services.AddSingleton(new NotesApiClient(httpClient));
        Services.AddSingleton(new TasksApiClient(httpClient));
        Services.AddSingleton(new CalendarApiClient(httpClient));
        Services.AddSingleton(new InventoryApiClient(httpClient));
        Services.AddSingleton(new PlacesApiClient(httpClient));
        Services.AddSingleton(new UsersApiClient(httpClient));
    }
}
