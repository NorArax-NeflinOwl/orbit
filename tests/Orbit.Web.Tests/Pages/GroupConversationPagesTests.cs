using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Core.Permissions;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// A group is a conversation like any other, so it opens on the chat page rather than on one of its
/// own. What is left over - the roster, and what the group itself is - lives on subpages the thread's
/// menu leads to, which is what these cover.
/// </summary>
public sealed class GroupConversationPagesTests : OrbitTestContext
{
    private static readonly Guid GroupId = Guid.NewGuid();
    private static readonly Guid OwnUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    public GroupConversationPagesTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        RegisterAuthentication();
        RegisterPermissions();
    }

    [Fact]
    public void A_group_has_no_page_of_its_own_any_more()
    {
        // The chat page answers the group routes itself. A separate page is what made switching between
        // a person and a group a change of screen.
        var chatRoutes = typeof(Chat)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Select(route => route.Template)
            .ToList();

        Assert.Contains("/chat/groups", chatRoutes);
        Assert.Contains("/chat/groups/{GroupId:guid}", chatRoutes);
        Assert.DoesNotContain(
            typeof(Chat).Assembly.GetTypes(), type => type.Name == "GroupChat");
    }

    [Fact]
    public void The_members_page_lists_everyone_in_the_group()
    {
        RegisterChatApi(ownRole: "Member");

        var cut = RenderMembers();

        Assert.Contains("Anna Kowalska", cut.Markup);
        Assert.Contains("You", cut.Markup);
    }

    [Fact]
    public void Only_an_admin_is_offered_the_controls_that_only_an_admin_may_use()
    {
        // The server refuses these anyway (see ChatGroup) - the button is left out so nobody is offered
        // something that would come back as a refusal.
        RegisterChatApi(ownRole: "Member");

        var cut = RenderMembers();

        Assert.DoesNotContain("Make admin", cut.Markup);
        Assert.DoesNotContain("Add someone", cut.Markup);
    }

    [Fact]
    public void An_admin_can_change_who_is_in_it()
    {
        RegisterChatApi(ownRole: "Admin");

        var cut = RenderMembers();

        Assert.Contains("Make admin", cut.Markup);
        Assert.Contains("Remove", cut.Markup);
    }

    [Fact]
    public void Anybody_can_show_themselves_out()
    {
        RegisterChatApi(ownRole: "Member");

        var cut = RenderMembers();

        Assert.Contains("Leave group", cut.Markup);
    }

    [Fact]
    public void The_members_page_leads_back_to_the_conversation()
    {
        RegisterChatApi(ownRole: "Member");
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderMembers();

        cut.FindAll(".page-header-actions button").First(button => button.TextContent.Contains("Back to chat")).Click();

        Assert.EndsWith($"/chat/groups/{GroupId}", navigationManager.Uri);
    }

    [Fact]
    public void The_info_page_says_what_the_group_is()
    {
        RegisterChatApi(ownRole: "Admin");

        var cut = RenderComponent<GroupInfo>(parameters => parameters.Add(page => page.GroupId, GroupId));

        Assert.Contains("Weekend trip", cut.Find("h1").TextContent);
        Assert.Contains("2 members", cut.Markup);
        Assert.Contains("Admin", cut.Markup);
    }

    [Fact]
    public void A_group_this_reader_is_no_longer_in_says_so()
    {
        RegisterChatApi(ownRole: "Member", includeGroup: false);

        var cut = RenderMembers();

        Assert.Contains("no longer one you're in", cut.Markup);
    }

    private IRenderedComponent<GroupMembers> RenderMembers()
        => RenderComponent<GroupMembers>(parameters => parameters.Add(page => page.GroupId, GroupId));

    private void RegisterChatApi(string ownRole, bool includeGroup = true)
    {
        var groupsJson = includeGroup
            ? $$"""
              [{"id":"{{GroupId}}","name":"Weekend trip","createdByUserId":"{{OwnUserId}}",
                "createdAtUtc":"2026-08-01T10:00:00+00:00","ownRole":"{{ownRole}}",
                "members":[{"userId":"{{OwnUserId}}","role":"{{ownRole}}","joinedAtUtc":"2026-08-01T10:00:00+00:00"},
                           {"userId":"{{OtherUserId}}","role":"Member","joinedAtUtc":"2026-08-01T10:00:00+00:00"}]}]
              """
            : "[]";
        var contactsJson = $$"""
            [{"userId":"{{OtherUserId}}","userName":"anna","displayName":"Anna Kowalska","email":"anna@example.com",
              "publicKeyBase64":"key","lastMessageAtUtc":"2026-08-01T10:00:00+00:00",
              "requiresApprovalFromCurrentUser":false,"isPendingApprovalFromOtherParty":false,"unreadCount":0,
              "presenceStatus":"Offline"}]
            """;

        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
            Json(request.RequestUri!.AbsolutePath.EndsWith("/groups", StringComparison.Ordinal)
                ? groupsJson
                : contactsJson)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new ChatApiClient(httpClient));
    }

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
    }

    private void RegisterPermissions()
    {
        var granted = $"\"{nameof(ApplicationPermission.Chat)}\"";
        var permissions = new UserPermissionState(new UsersApiClient(
            new HttpClient(new StubHttpMessageHandler(_ => Json($"{{\"granted\":[{granted}]}}")))
            {
                BaseAddress = new Uri("https://example.test/")
            }));
        permissions.EnsureLoadedAsync().GetAwaiter().GetResult();
        Services.AddSingleton(permissions);
    }

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string CreateUnsignedJwt(Dictionary<string, string> claims)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(claims));
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
