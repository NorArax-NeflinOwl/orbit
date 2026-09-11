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

    /// <summary>
    /// Leaving is asked about first, as it is on the roster - it used to go on the click, which for a
    /// group's only admin handed the group to whoever the server chose. Only the confirmation tells the
    /// server.
    /// </summary>
    [Fact]
    public void Leaving_a_group_asks_first_and_then_tells_the_server()
    {
        Register(contacts: [], groups: [Group("Wyjazd", isArchived: true)]);

        var cut = RenderComponent<Web.Pages.Contacts>();
        OpenTheArchiveTab(cut);
        ChooseToLeave(cut);

        Assert.DoesNotContain(_requestedPaths, path => path.EndsWith("/membership", StringComparison.Ordinal));
        cut.Find(".group-leave-confirmation .btn-danger").Click();

        Assert.Contains(_requestedPaths, path => path.EndsWith("/membership", StringComparison.Ordinal));
    }

    /// <summary>
    /// The same question the roster asks, not a lesser copy: the only admin leaving people behind is asked
    /// who takes over, with the longest-standing member already chosen - listed second here, so a picker
    /// that defaulted to the first row would fail - and the choice is what is sent.
    /// </summary>
    [Fact]
    public void The_only_admin_leaving_from_the_archive_is_asked_who_takes_over()
    {
        var ownUserId = Guid.NewGuid();
        var newerUserId = Guid.NewGuid();
        var longestStandingUserId = Guid.NewGuid();
        SignIn(ownUserId);
        Register(contacts: [], groups: [Group("Wyjazd", isArchived: true, ownRole: "Admin", members:
        [
            (ownUserId, "Admin", "2026-07-01T10:00:00+00:00"),
            (newerUserId, "Member", "2026-07-20T10:00:00+00:00"),
            (longestStandingUserId, "Member", "2026-07-05T10:00:00+00:00")
        ])]);

        var cut = RenderComponent<Web.Pages.Contacts>();
        OpenTheArchiveTab(cut);
        ChooseToLeave(cut);

        Assert.Equal(longestStandingUserId.ToString(), cut.Find("#successorInput").GetAttribute("value"));
        cut.Find(".group-leave-confirmation .btn-danger").Click();

        Assert.Contains(_requestedQueries, query => query == $"?successorUserId={longestStandingUserId}");
    }

    [Fact]
    public void Cancelling_the_question_leaves_nobody()
    {
        Register(contacts: [], groups: [Group("Wyjazd", isArchived: true)]);

        var cut = RenderComponent<Web.Pages.Contacts>();
        OpenTheArchiveTab(cut);
        ChooseToLeave(cut);
        cut.Find(".group-leave-confirmation .btn-secondary").Click();

        Assert.Empty(cut.FindAll(".group-leave-confirmation"));
        Assert.DoesNotContain(_requestedPaths, path => path.EndsWith("/membership", StringComparison.Ordinal));
    }

    private void OpenTheArchiveTab(IRenderedComponent<Web.Pages.Contacts> cut)
        => cut.FindAll(".contacts-tab").Single(tab => tab.TextContent.Contains("Archive")).Click();

    private static void ChooseToLeave(IRenderedComponent<Web.Pages.Contacts> cut)
    {
        cut.Find(".person-row .overflow-menu-trigger").Click();
        cut.FindAll(".avatar-dropdown-item")
            .Single(item => item.TextContent.Contains("Leave and delete chat history")).Click();
    }

    /// <summary>The query string of every request, beside the paths - which is where a successor travels.</summary>
    private readonly List<string> _requestedQueries = [];

    /// <summary>
    /// Registered over the base's signed-out provider, so the leave question knows which member is the
    /// reader. The later registration is the one that resolves.
    /// </summary>
    private void SignIn(Guid userId)
    {
        var tokenStore = new TokenStore(new StubJSRuntime());
        tokenStore.SetTokenAsync(Components.GroupLeaveConfirmationTests.SignedInAs(userId)).GetAwaiter().GetResult();
        var refreshHttpClient = new HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new OrbitAuthenticationStateProvider(
            tokenStore, new TokenRefreshService(tokenStore, refreshHttpClient)));
    }

    private static string Contact(string displayName, bool isArchived)
        => $$"""
        {"userId":"{{Guid.NewGuid()}}","userName":"{{displayName.ToLowerInvariant()}}","displayName":"{{displayName}}",
         "email":"{{displayName.ToLowerInvariant()}}@example.test","publicKeyBase64":"key",
         "lastMessageAtUtc":"2026-08-01T10:00:00+00:00","requiresApprovalFromCurrentUser":false,
         "isPendingApprovalFromOtherParty":false,"unreadCount":0,"presenceStatus":"Offline",
         "isArchived":{{(isArchived ? "true" : "false")}}}
        """;

    private static string Group(
        string name, bool isArchived, string ownRole = "Member",
        (Guid UserId, string Role, string JoinedAtUtc)[]? members = null)
    {
        var membersJson = string.Join(",", (members ?? []).Select(member =>
            $$"""{"userId":"{{member.UserId}}","role":"{{member.Role}}","joinedAtUtc":"{{member.JoinedAtUtc}}"}"""));

        return $$"""
        {"id":"{{Guid.NewGuid()}}","name":"{{name}}","ownRole":"{{ownRole}}","members":[{{membersJson}}],
         "lastMessageAtUtc":"2026-08-01T10:00:00+00:00","unreadCount":0,
         "isArchived":{{(isArchived ? "true" : "false")}}}
        """;
    }

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
            _requestedQueries.Add(request.RequestUri.Query);
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
