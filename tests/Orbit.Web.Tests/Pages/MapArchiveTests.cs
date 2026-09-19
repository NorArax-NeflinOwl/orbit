using System.Net;
using System.Text;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// Where a place goes when its owner is finished with it, and the only page in Orbit that deletes one -
/// see MapArchive, asked for on 2026-09-19. A page of its own rather than a tab, because the map has
/// none: a place is not filed in folders the way the four kinds of card are.
/// </summary>
public sealed class MapArchiveTests : OrbitTestContext
{
    private string _placesJson = "[]";
    private string _grantedPermissionsJson = "{\"granted\":[]}";
    private readonly List<string> _archivedPaths = [];
    private readonly List<string> _deletedPaths = [];

    public MapArchiveTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        RegisterEverythingThePageAsksFor();
    }

    [Fact]
    public void The_archive_is_offered_only_to_an_account_that_has_unlocked_locations()
    {
        var cut = RenderComponent<MapArchive>();

        Assert.Empty(cut.FindAll(".page-header"));
    }

    /// <summary>Only what has been put away: the map draws the rest, and this page draws what it does not.</summary>
    [Fact]
    public void Only_the_places_put_away_are_listed()
    {
        GrantLocations();
        _placesJson = Together(APlace("The good bakery"), APlace("Last year's flat", isArchived: true));

        var cut = RenderComponent<MapArchive>();

        var rows = cut.FindAll(".map-archive-row").Select(row => row.TextContent).ToList();
        Assert.Single(rows);
        Assert.Contains("Last year's flat", rows[0], StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_archive_says_so()
    {
        GrantLocations();

        var cut = RenderComponent<MapArchive>();

        Assert.Contains("Nothing has been put away", cut.Find(".empty-list").TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// Bringing one back is the press this page is mostly read for, and it goes back under whatever it
    /// was filed under - archiving leaves that alone, so nothing has to be chosen here.
    /// </summary>
    [Fact]
    public void A_place_is_brought_back_to_the_map()
    {
        GrantLocations();
        _placesJson = Together(APlace("Last year's flat", isArchived: true));
        var cut = RenderComponent<MapArchive>();

        cut.FindAll(".map-archive-row button").First(button => button.TextContent.Trim() == "Put back").Click();

        Assert.Contains(_archivedPaths, path => path.EndsWith("/archived", StringComparison.Ordinal));
    }

    /// <summary>
    /// Deleting is asked about first, because it is the one press here that cannot be undone - putting
    /// away can. The question names the place, since a list of archived places is read months later.
    /// </summary>
    [Fact]
    public void Deleting_a_place_is_asked_about_first()
    {
        GrantLocations();
        _placesJson = Together(APlace("Last year's flat", isArchived: true));
        var cut = RenderComponent<MapArchive>();

        cut.FindAll(".map-archive-row button").First(button => button.TextContent.Trim() == "Delete").Click();

        Assert.Empty(_deletedPaths);
        var dialog = cut.Find(".dialog-panel");
        Assert.Contains("Last year's flat", dialog.TextContent, StringComparison.Ordinal);

        dialog.QuerySelectorAll("button").First(button => button.TextContent.Trim() == "Delete").Click();

        Assert.Single(_deletedPaths);
    }

    private void GrantLocations() => _grantedPermissionsJson = "{\"granted\":[\"Location\"]}";

    private static string APlace(string name, bool isArchived = false)
        => "{\"id\":\"" + Guid.NewGuid() + "\",\"name\":\"" + name + "\",\"description\":\"\","
        + "\"where\":{\"address\":\"Piękna 1\",\"latitude\":52.2,\"longitude\":21.0},"
        + "\"colour\":\"\",\"priority\":\"Normal\",\"taskListIds\":[],"
        + "\"createdAtUtc\":\"2026-09-10T10:00:00+00:00\",\"updatedAtUtc\":\"2026-09-10T10:00:00+00:00\","
        + "\"isShared\":false,\"sharedByUserName\":null,\"accessLevel\":\"CanEdit\","
        + "\"isSharedWithOthers\":false,\"isPrivate\":false,"
        + "\"isArchived\":" + (isArchived ? "true" : "false") + ",\"sourceTaskItemId\":null}";

    private static string Together(params string[] places) => "[" + string.Join(",", places) + "]";

    private void RegisterEverythingThePageAsksFor()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Delete)
            {
                _deletedPaths.Add(path);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (request.Method == HttpMethod.Put && path.EndsWith("/archived", StringComparison.Ordinal))
            {
                _archivedPaths.Add(path);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path.EndsWith("/permissions", StringComparison.Ordinal))
            {
                return Text(_grantedPermissionsJson);
            }

            if (path.EndsWith("/places", StringComparison.Ordinal))
            {
                return Text(_placesJson);
            }

            // The account itself, which UserPermissionState reads what it is allowed from.
            return Text(
                "{\"id\":\"" + Guid.NewGuid() + "\",\"email\":\"owner@example.com\",\"userName\":\"owner\","
                + "\"displayName\":\"Owner\",\"isEmailVerified\":true,\"hasPassword\":true,"
                + "\"isGoogleLinked\":false,\"location\":null}");
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        var usersApiClient = new UsersApiClient(httpClient);
        Services.AddSingleton(usersApiClient);
        Services.AddSingleton(new PlacesApiClient(httpClient));
        Services.AddSingleton(new UserPermissionState(usersApiClient));
        Services.AddAuthorizationCore();
    }

    private static HttpResponseMessage Text(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
