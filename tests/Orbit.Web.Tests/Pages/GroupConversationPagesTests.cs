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
using Orbit.Web.Components;
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

    /// <summary>A contact who is not in the group, so there is somebody an admin could actually add.</summary>
    private static readonly Guid AddableUserId = Guid.NewGuid();

    /// <summary>
    /// Kept rather than resolved back out of the container: bUnit freezes its service collection the
    /// first time anything is read from it, so a later registration would fail.
    /// </summary>
    private readonly OrbitAuthenticationStateProvider _authenticationStateProvider;

    public GroupConversationPagesTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        _authenticationStateProvider = RegisterAuthentication();
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

        var menu = OpenTheMenuFor(cut, OtherUserId);

        Assert.False(ItemSaying(menu, "Make admin").HasAttribute("disabled"));
        Assert.False(ItemSaying(menu, "Remove").HasAttribute("disabled"));
    }

    /// <summary>
    /// The roster is read down the names, so the actions live behind the same three-dot menu every card
    /// in the app carries rather than as a row of buttons whose widths pushed the names about.
    /// </summary>
    [Fact]
    public void A_members_actions_are_behind_one_menu_rather_than_buttons_on_the_row()
    {
        RegisterChatApi(ownRole: "Admin");

        var cut = RenderMembers();

        Assert.Empty(cut.FindAll(".group-member-row .btn-secondary"));
        // One menu per member, and this group has two.
        Assert.Equal(2, cut.FindAll(".group-member-menu .overflow-menu-trigger").Count);
    }

    /// <summary>
    /// Greyed rather than left out: an option that disappears looks like an option that does not exist,
    /// and "you are not an admin here" is worth saying. Each greyed entry carries the reason on itself,
    /// where the pointer already is.
    /// </summary>
    [Fact]
    public void Somebody_who_is_not_an_admin_sees_the_options_greyed_and_told_why()
    {
        RegisterChatApi(ownRole: "Member");
        var cut = RenderMembers();

        var menu = OpenTheMenuFor(cut, OtherUserId);

        var promote = ItemSaying(menu, "Make admin");
        var remove = ItemSaying(menu, "Remove");
        Assert.True(promote.HasAttribute("disabled"));
        Assert.True(remove.HasAttribute("disabled"));
        Assert.Equal("Only a group admin can change who is in it.", promote.GetAttribute("title"));
        Assert.Equal("Only a group admin can change who is in it.", remove.GetAttribute("title"));
    }

    /// <summary>
    /// And nobody changes their own standing, admin or not - an admin who could demote themselves could
    /// leave a group with nobody able to change it. The server refuses it too.
    /// </summary>
    [Fact]
    public void An_admin_cannot_change_their_own_standing()
    {
        RegisterChatApi(ownRole: "Admin");
        var cut = RenderMembers();

        var menu = OpenTheMenuFor(cut, OwnUserId);

        var demote = ItemSaying(menu, "Demote");
        Assert.True(demote.HasAttribute("disabled"));
        Assert.Equal("Your own standing in a group is not yours to change.", demote.GetAttribute("title"));
    }

    /// <summary>Who somebody is needs no standing, so it is the one entry never greyed.</summary>
    [Fact]
    public void Info_is_offered_whoever_is_reading()
    {
        RegisterChatApi(ownRole: "Member");
        var cut = RenderMembers();

        var menu = OpenTheMenuFor(cut, OtherUserId);
        ItemSaying(menu, "Info").Click();

        Assert.EndsWith($"/contacts/{OtherUserId}", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void Handing_over_the_history_is_offered_to_an_admin_and_is_off_until_asked_for()
    {
        RegisterChatApi(ownRole: "Admin", includeAddableContact: true);

        var cut = RenderMembers();

        // Everything said in the group so far was said to the people who were in it, so passing it on is
        // a decision somebody makes rather than what happens if they don't look.
        var shareHistory = cut.Find("#shareHistoryInput");
        Assert.False(shareHistory.HasAttribute("checked"));
        Assert.Contains("re-encrypts each message", cut.Markup);
    }

    [Fact]
    public void Somebody_who_cannot_add_members_is_not_offered_their_history_either()
    {
        // The share only ever runs on the back of an add, and the server refuses it from anyone who is
        // not an admin - offering the checkbox alone would be offering half an action.
        RegisterChatApi(ownRole: "Member", includeAddableContact: true);

        var cut = RenderMembers();

        Assert.Empty(cut.FindAll("#shareHistoryInput"));
    }

    [Fact]
    public void Anybody_can_show_themselves_out()
    {
        RegisterChatApi(ownRole: "Member");
        var cut = RenderMembers();

        var menu = OpenTheMenuFor(cut, OwnUserId);

        // Showing yourself out is not the same act as removing somebody, so on your own row this is the
        // way out of the group rather than a greyed "Remove".
        Assert.False(ItemSaying(menu, "Leave group").HasAttribute("disabled"));
        Assert.DoesNotContain(menu.QuerySelectorAll(".avatar-dropdown-item"), item => item.TextContent.Trim() == "Remove");
    }

    /// <summary>
    /// The row for one member, with its menu opened. Found again after the press rather than kept: the
    /// dropdown only exists once the menu is open, so the element read before it was there holds none
    /// of the entries this asks about.
    /// </summary>
    private static AngleSharp.Dom.IElement OpenTheMenuFor(IRenderedFragment cut, Guid memberUserId)
    {
        RowFor(cut, memberUserId).QuerySelector(".overflow-menu-trigger")!.Click();
        return RowFor(cut, memberUserId);
    }

    /// <summary>Which row is whose, by the colour their avatar is drawn in - see AvatarHelper.</summary>
    private static AngleSharp.Dom.IElement RowFor(IRenderedFragment cut, Guid memberUserId)
        => cut.FindAll(".group-member-row")
            .First(candidate => candidate.QuerySelector(".avatar-sm")!
                .GetAttribute("style")!.Contains(AvatarHelper.AvatarColor(memberUserId), StringComparison.Ordinal));

    private static AngleSharp.Dom.IElement ItemSaying(AngleSharp.Dom.IElement menu, string label)
        => menu.QuerySelectorAll(".avatar-dropdown-item").First(item => item.TextContent.Trim() == label);

    /// <summary>
    /// The roster's own heading and nothing else. The Back button that used to sit beside it did what
    /// the browser's own does, and every page here had grown one.
    /// </summary>
    [Fact]
    public void The_members_page_carries_no_control_but_its_own()
    {
        RegisterChatApi(ownRole: "Member");

        var cut = RenderMembers();

        Assert.Empty(cut.FindAll(".page-header-actions"));
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

    /// <summary>
    /// The roster is reached by the line that counts it. A button beside the page's name said the same
    /// thing twice, and the count is the half somebody is already reading when they want to know who
    /// is in it.
    /// </summary>
    [Fact]
    public void The_member_count_is_the_way_to_the_roster()
    {
        RegisterChatApi(ownRole: "Member");
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<GroupInfo>(parameters => parameters.Add(page => page.GroupId, GroupId));

        cut.Find(".row-meta-opens").Click();

        Assert.EndsWith($"/chat/groups/{GroupId}/members", navigationManager.Uri);
    }

    private IRenderedComponent<GroupMembers> RenderMembers()
        => RenderComponent<GroupMembers>(parameters => parameters.Add(page => page.GroupId, GroupId));

    private void RegisterChatApi(string ownRole, bool includeGroup = true, bool includeAddableContact = false)
    {
        var groupsJson = includeGroup
            ? $$"""
              [{"id":"{{GroupId}}","name":"Weekend trip","createdByUserId":"{{OwnUserId}}",
                "createdAtUtc":"2026-08-01T10:00:00+00:00","ownRole":"{{ownRole}}",
                "members":[{"userId":"{{OwnUserId}}","role":"{{ownRole}}","joinedAtUtc":"2026-08-01T10:00:00+00:00"},
                           {"userId":"{{OtherUserId}}","role":"Member","joinedAtUtc":"2026-08-01T10:00:00+00:00"}]}]
              """
            : "[]";
        var addableContactJson = includeAddableContact
            ? $$"""
              ,{"userId":"{{AddableUserId}}","userName":"piotr","displayName":"Piotr Nowak","email":"piotr@example.com",
               "publicKeyBase64":"key","lastMessageAtUtc":"2026-08-01T10:00:00+00:00",
               "requiresApprovalFromCurrentUser":false,"isPendingApprovalFromOtherParty":false,"unreadCount":0,
               "presenceStatus":"Offline"}
              """
            : string.Empty;
        var contactsJson = $$"""
            [{"userId":"{{OtherUserId}}","userName":"anna","displayName":"Anna Kowalska","email":"anna@example.com",
              "publicKeyBase64":"key","lastMessageAtUtc":"2026-08-01T10:00:00+00:00",
              "requiresApprovalFromCurrentUser":false,"isPendingApprovalFromOtherParty":false,"unreadCount":0,
              "presenceStatus":"Offline"}{{addableContactJson}}]
            """;

        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
            Json(request.RequestUri!.AbsolutePath.EndsWith("/groups", StringComparison.Ordinal)
                ? groupsJson
                : contactsJson)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var chatApiClient = new ChatApiClient(httpClient);
        Services.AddSingleton(chatApiClient);
        RegisterHistorySharing(chatApiClient, httpClient);
    }

    /// <summary>
    /// The members page asks for this so it can hand a newcomer the conversation so far. Built from the
    /// same stub transport as everything else here: these tests render the page rather than exercise the
    /// crypto, which needs a real browser and is covered where the server side of it lives.
    /// </summary>
    private void RegisterHistorySharing(ChatApiClient chatApiClient, HttpClient httpClient)
    {
        var jsRuntime = new StubJSRuntime();
        var usersApiClient = new UsersApiClient(httpClient);
        var ownEncryptionKeyProvider = new OwnEncryptionKeyProvider(jsRuntime, usersApiClient, _authenticationStateProvider);

        Services.AddSingleton(new GroupHistorySharing(
            chatApiClient,
            new EncryptedChatMessageReader(usersApiClient, ownEncryptionKeyProvider, jsRuntime),
            new EncryptedChatMessageSender(jsRuntime, ownEncryptionKeyProvider, usersApiClient, chatApiClient)));
    }

    private OrbitAuthenticationStateProvider RegisterAuthentication()
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
        return authenticationStateProvider;
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
