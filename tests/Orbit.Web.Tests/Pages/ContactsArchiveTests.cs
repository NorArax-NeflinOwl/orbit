using System.Net;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Core.Permissions;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// What putting a conversation away does to the Contacts page. The point of archiving is that the row
/// leaves the list somebody reads every day and is still findable afterwards - a row that stays put
/// makes the whole thing pointless, and one that vanishes for good is a deletion nobody asked for.
/// </summary>
public sealed class ContactsArchiveTests : OrbitTestContext
{
    private readonly List<string> _requestedPaths = [];

    public ContactsArchiveTests() => Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

    [Fact]
    public void Somebody_put_away_is_off_the_working_lists()
    {
        Register(contacts: [Contact("Anna", isArchived: true), Contact("Bartek", isArchived: false)]);

        var cut = RenderComponent<Web.Pages.Contacts>();

        // Chats is the tab the page opens on.
        Assert.Contains("Bartek", cut.Markup);
        Assert.DoesNotContain("Anna", cut.Markup);
    }

    [Fact]
    public void And_is_on_the_archive_tab_instead()
    {
        Register(contacts: [Contact("Anna", isArchived: true), Contact("Bartek", isArchived: false)]);

        var cut = RenderComponent<Web.Pages.Contacts>();
        OpenTheArchiveTab(cut);

        Assert.Contains("Anna", cut.Markup);
        Assert.DoesNotContain("Bartek", cut.Markup);
    }

    /// <summary>
    /// A group that has been put away goes to the same place a person does. The archive is one list,
    /// because somebody wondering where a conversation went is not thinking about which kind it was.
    /// </summary>
    [Fact]
    public void A_group_put_away_lands_in_the_same_archive()
    {
        Register(contacts: [], groups: [Group("Wyjazd", isArchived: true), Group("Dom", isArchived: false)]);

        var cut = RenderComponent<Web.Pages.Contacts>();
        OpenTheArchiveTab(cut);

        Assert.Contains("Wyjazd", cut.Markup);
        Assert.DoesNotContain("Dom", cut.Markup);
    }

    [Fact]
    public void An_empty_archive_is_not_offered_at_all()
    {
        Register(contacts: [Contact("Bartek", isArchived: false)]);

        var cut = RenderComponent<Web.Pages.Contacts>();

        // A tab whose answer is "nothing" still has to be opened to say so.
        Assert.DoesNotContain(cut.FindAll(".contacts-tab"), tab => tab.TextContent.Contains("Archive"));
    }

    [Fact]
    public void Putting_somebody_away_tells_the_server()
    {
        Register(contacts: [Contact("Bartek", isArchived: false)]);

        var cut = RenderComponent<Web.Pages.Contacts>();
        cut.Find(".person-row .overflow-menu-trigger").Click();
        cut.FindAll(".avatar-dropdown-item").Single(item => item.TextContent.Contains("Archive")).Click();

        Assert.Contains(_requestedPaths, path => path.EndsWith("/archived", StringComparison.Ordinal));
    }


    /// <summary>
    /// The archive is where somebody has already decided they are done with a conversation, so it is
    /// where the two endings are offered: emptying it, and - for a group - walking out of it.
    /// </summary>
    [Fact]
    public void The_archive_offers_the_two_ways_of_being_done_with_a_conversation()
    {
        Register(contacts: [Contact("Anna", isArchived: true)], groups: [Group("Wyjazd", isArchived: true)]);

        var cut = RenderComponent<Web.Pages.Contacts>();
        OpenTheArchiveTab(cut);

        // A menu's items exist only while it is open, so each row is opened in turn. The person's row
        // comes first, the group's after it.
        cut.FindAll(".person-row .overflow-menu-trigger").First().Click();
        Assert.Contains("Delete chat history", cut.Markup);

        cut.FindAll(".person-row .overflow-menu-trigger").Last().Click();
        Assert.Contains("Leave and delete chat history", cut.Markup);
    }

    [Fact]
    public void Emptying_a_conversation_tells_the_server()
    {
        Register(contacts: [Contact("Anna", isArchived: true)]);

        var cut = RenderComponent<Web.Pages.Contacts>();
        OpenTheArchiveTab(cut);
        cut.Find(".person-row .overflow-menu-trigger").Click();
        cut.FindAll(".avatar-dropdown-item").Single(item => item.TextContent.Contains("Delete chat history")).Click();

        Assert.Contains(_requestedPaths, path => path.EndsWith("/messages", StringComparison.Ordinal));
    }

    [Fact]
    public void Leaving_a_group_tells_the_server()
    {
        Register(contacts: [], groups: [Group("Wyjazd", isArchived: true)]);

        var cut = RenderComponent<Web.Pages.Contacts>();
        OpenTheArchiveTab(cut);
        cut.Find(".person-row .overflow-menu-trigger").Click();
        cut.FindAll(".avatar-dropdown-item")
            .Single(item => item.TextContent.Contains("Leave and delete chat history")).Click();

        Assert.Contains(_requestedPaths, path => path.EndsWith("/membership", StringComparison.Ordinal));
    }

    private void OpenTheArchiveTab(IRenderedComponent<Web.Pages.Contacts> cut)
        => cut.FindAll(".contacts-tab").Single(tab => tab.TextContent.Contains("Archive")).Click();

    private static string Contact(string displayName, bool isArchived)
        => $$"""
        {"userId":"{{Guid.NewGuid()}}","userName":"{{displayName.ToLowerInvariant()}}","displayName":"{{displayName}}",
         "email":"{{displayName.ToLowerInvariant()}}@example.test","publicKeyBase64":"key",
         "lastMessageAtUtc":"2026-08-01T10:00:00+00:00","requiresApprovalFromCurrentUser":false,
         "isPendingApprovalFromOtherParty":false,"unreadCount":0,"presenceStatus":"Offline",
         "isArchived":{{(isArchived ? "true" : "false")}}}
        """;

    private static string Group(string name, bool isArchived)
        => $$"""
        {"id":"{{Guid.NewGuid()}}","name":"{{name}}","members":[],
         "lastMessageAtUtc":"2026-08-01T10:00:00+00:00","unreadCount":0,
         "isArchived":{{(isArchived ? "true" : "false")}}}
        """;

    /// <summary>
    /// Answers the contact list, the group list, and the archive call itself, recording every path so a
    /// test can say whether the page actually asked rather than only that it redrew.
    /// </summary>
    private void Register(string[] contacts, string[]? groups = null)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            _requestedPaths.Add(path);
            var body = path.Contains("/groups", StringComparison.Ordinal)
                ? $"[{string.Join(",", groups ?? [])}]"
                : $"[{string.Join(",", contacts)}]";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new ChatApiClient(httpClient));
        Services.AddSingleton(new UsersApiClient(httpClient));

        var permissionsHandler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"{{\"granted\":[\"{nameof(ApplicationPermission.Contacts)}\",\"{nameof(ApplicationPermission.Chat)}\"]}}",
                Encoding.UTF8,
                "application/json")
        });
        var permissions = new UserPermissionState(
            new UsersApiClient(new HttpClient(permissionsHandler) { BaseAddress = new Uri("https://example.test/") }));
        permissions.EnsureLoadedAsync().GetAwaiter().GetResult();
        Services.AddSingleton(permissions);
    }
}
