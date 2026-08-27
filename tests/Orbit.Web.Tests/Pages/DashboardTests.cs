using System.Net;
using Orbit.Core.Permissions;
using System.Text;
using System.Net.Http.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Chat;
using Orbit.Contracts.Users;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Tasks;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Orbit.Web.Tests;
using Xunit;

namespace Orbit.Web.Tests.Pages;

public sealed class DashboardTests : OrbitTestContext
{
    public DashboardTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        RegisterUsersApiClient();
        // Notes/task lists/events aren't what these tests exercise - each is stubbed to an empty list so
        // the dashboard finishes loading without depending on unrelated fixture data.
        RegisterEmptyNotesApiClient();
        RegisterEmptyTasksApiClient();
        RegisterEmptyCalendarApiClient();
        RegisterDashboardPins();
        RegisterPermissions();
    }

    /// <summary>
    /// The dashboard only asks for chats, groups and shared positions once this account has unlocked
    /// them (see UserPermissionState), so these tests grant everything - what they are about is what the
    /// columns show, not what is unlocked. PermissionsTests below covers the locked case.
    /// </summary>
    private void RegisterPermissions(params ApplicationPermission[] granted)
    {
        var names = (granted.Length > 0 ? granted : Enum.GetValues<ApplicationPermission>())
            .Select(permission => $"\"{permission}\"");
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"{{\"granted\":[{string.Join(",", names)}]}}", Encoding.UTF8, "application/json")
        });
        var permissions = new UserPermissionState(
            new UsersApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));
        permissions.RefreshAsync().GetAwaiter().GetResult();
        Services.AddSingleton(permissions);
    }

    /// <summary>
    /// Which cards are pinned lives in localStorage, reached through a JS module (see
    /// DashboardPinService) - stubbed as "nothing pinned" so these tests see the cards in their written
    /// order rather than an order somebody's browser happened to save.
    /// </summary>
    private void RegisterDashboardPins()
    {
        var module = JSInterop.SetupModule("./js/dashboardPins.js");
        module.Setup<string[]>("getPinnedCards").SetResult([]);
        module.SetupVoid("setPinnedCards", _ => true);
        Services.AddScoped<DashboardPinService>();
    }

    [Fact]
    public void A_contact_pending_approval_from_the_other_party_shows_only_in_the_chats_column()
    {
        var approvedContact = new ContactDto(
            Guid.NewGuid(), "anna", "Anna Kowalska", "anna@example.com", "public-key", DateTimeOffset.UtcNow,
            RequiresApprovalFromCurrentUser: false, IsPendingApprovalFromOtherParty: false);
        var pendingContact = new ContactDto(
            Guid.NewGuid(), "bartek", "Bartek Nowak", "bartek@example.com", "public-key", DateTimeOffset.UtcNow,
            RequiresApprovalFromCurrentUser: false, IsPendingApprovalFromOtherParty: true);
        RegisterChatApiClient([approvedContact, pendingContact]);

        var cut = RenderComponent<Dashboard>();

        var chatsColumnText = FindColumn(cut, "Recent chats").TextContent;
        var contactsColumnText = FindColumn(cut, "Contacts").TextContent;
        Assert.Contains("Anna Kowalska", chatsColumnText);
        Assert.Contains("Bartek Nowak", chatsColumnText);
        Assert.Contains("Anna Kowalska", contactsColumnText);
        Assert.DoesNotContain("Bartek Nowak", contactsColumnText);
    }

    [Fact]
    public void Group_chats_get_their_own_column()
    {
        RegisterChatApiClient([], [Group("Weekend trip", memberCount: 3), Group("Book club", memberCount: 5)]);

        var cut = RenderComponent<Dashboard>();

        var groupsColumn = FindColumn(cut, "Groups").TextContent;
        Assert.Contains("Weekend trip", groupsColumn);
        Assert.Contains("Book club", groupsColumn);
    }

    [Fact]
    public void A_group_you_administer_says_so()
    {
        RegisterChatApiClient([], [Group("Weekend trip", memberCount: 3, ownRole: "Admin")]);

        var cut = RenderComponent<Dashboard>();

        Assert.Contains("Admin", FindColumn(cut, "Groups").TextContent);
    }

    [Fact]
    public void An_account_in_no_groups_gets_no_groups_column()
    {
        RegisterChatApiClient([], []);

        var cut = RenderComponent<Dashboard>();

        // A column that only ever said "none" would be dead space on the one page meant to be scanned.
        Assert.DoesNotContain("Groups", cut.Markup);
    }

    [Fact]
    public void An_account_that_has_unlocked_nothing_still_gets_its_notes_and_tasks()
    {
        // What this pins: the dashboard is one page built from several separate questions, and the ones
        // this account may not ask must not take the page down with them. Before the permission gate
        // had this, a 403 on contacts or shared positions turned the entire dashboard - notes and task
        // lists included - into "Couldn't load the dashboard", which is what everybody saw on the
        // release that introduced it.
        Services.Remove(Services.Single(service => service.ServiceType == typeof(UserPermissionState)));
        RegisterPermissions(ApplicationPermission.Sharing);
        RegisterNotesApiClient([new NoteDto(
            Guid.NewGuid(), "Shopping", [], IsPrivate: false, EncryptedContent: null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null)]);
        RegisterChatApiClient([new ContactDto(
            Guid.NewGuid(), "anna", "Anna Kowalska", "anna@example.com", "public-key", DateTimeOffset.UtcNow,
            RequiresApprovalFromCurrentUser: false, IsPendingApprovalFromOtherParty: false)]);

        var cut = RenderComponent<Dashboard>();

        Assert.Contains("Shopping", cut.Markup);
        Assert.DoesNotContain("Couldn't load the dashboard", cut.Markup);
        // Nothing was asked for, so nothing is claimed: no contacts column rather than an empty one.
        Assert.DoesNotContain("Anna Kowalska", cut.Markup);
    }

    [Fact]
    public void Clicking_a_group_opens_it()
    {
        var group = Group("Weekend trip", memberCount: 3);
        RegisterChatApiClient([], [group]);
        var cut = RenderComponent<Dashboard>();

        FindColumn(cut, "Groups").QuerySelector(".list-row")!.Click();

        Assert.EndsWith($"/chat/groups/{group.Id}", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void Clicking_a_task_list_opens_its_checklist_rather_than_its_settings()
    {
        var taskList = TaskList("Errands");
        RegisterChatApiClient([]);
        RegisterEmptyNotesApiClient();
        RegisterTasksApiClient([taskList]);
        RegisterEmptyCalendarApiClient();
        var cut = RenderComponent<Dashboard>();

        FindColumn(cut, "Tasks").QuerySelector(".list-row")!.Click();

        // Clicking a list here means "let me get on with it", which is ticking things off - reworking
        // the list's own settings is a deliberate trip to the editor from there.
        Assert.EndsWith($"/tasks/{taskList.Id}/checklist", Services.GetRequiredService<NavigationManager>().Uri);
    }

    private static ChatGroupDto Group(string name, int memberCount, string ownRole = "Member")
        => new(
            Guid.NewGuid(), name, Guid.NewGuid(), DateTimeOffset.UtcNow, ownRole,
            Enumerable.Range(0, memberCount)
                .Select(_ => new ChatGroupMemberDto(Guid.NewGuid(), "Member", DateTimeOffset.UtcNow))
                .ToList());

    private static IElement FindColumn(IRenderedComponent<Dashboard> cut, string heading)
        => cut.FindAll("div.card").Single(column => column.QuerySelector(".card-title")!.TextContent == heading);

    /// <summary>
    /// The dashboard asks this client for two different things - contacts and groups - so the stub has
    /// to answer by path. Answering everything with contacts would deserialize them as groups too, and
    /// the groups card would render entries with no name.
    /// </summary>
    private void RegisterChatApiClient(IReadOnlyList<ContactDto> contacts, IReadOnlyList<ChatGroupDto>? groups = null)
    {
        var handler = new StubHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/groups", StringComparison.Ordinal)
                ? JsonResponse(groups ?? [])
                : JsonResponse(contacts));
        Services.AddSingleton(new ChatApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));
    }

    /// <summary>
    /// The dashboard asks who is sharing a position with the reader. These tests are about the other
    /// columns, so it answers "nobody" - the column then draws nothing, which is what they expect.
    /// </summary>
    private void RegisterUsersApiClient() => RegisterSharedLocations([]);

    private void RegisterSharedLocations(IReadOnlyList<SharedLocationDto> shares)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(shares)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new UsersApiClient(httpClient));
    }

    [Fact]
    public void Somebody_sharing_their_position_shows_up_on_the_dashboard()
    {
        var sharerId = Guid.NewGuid();
        RegisterChatApiClient([new ContactDto(
            sharerId, "anna", "Anna Kowalska", "anna@example.com", "public-key", DateTimeOffset.UtcNow,
            RequiresApprovalFromCurrentUser: false, IsPendingApprovalFromOtherParty: false)]);
        RegisterSharedLocations([new SharedLocationDto(
            sharerId, Guid.NewGuid(), "cipher", "nonce", IsContinuous: true, DateTimeOffset.UtcNow)]);

        var cut = RenderComponent<Web.Pages.Dashboard>();

        // The name and that it is live - not the position itself, which only the map page can open.
        Assert.Contains("Anna Kowalska", cut.Markup);
        Assert.Contains("Shared with you", cut.Markup);
    }

    [Fact]
    public void Nobody_sharing_a_position_gets_no_column_for_it()
    {
        RegisterChatApiClient([new ContactDto(
            Guid.NewGuid(), "anna", "Anna Kowalska", "anna@example.com", "public-key", DateTimeOffset.UtcNow,
            RequiresApprovalFromCurrentUser: false, IsPendingApprovalFromOtherParty: false)]);

        var cut = RenderComponent<Web.Pages.Dashboard>();

        Assert.DoesNotContain("Shared with you", cut.Markup);
    }

    private void RegisterEmptyNotesApiClient() => RegisterNotesApiClient([]);

    private void RegisterNotesApiClient(IReadOnlyList<NoteDto> notes)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(notes))) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new NotesApiClient(httpClient));
    }

    private void RegisterEmptyTasksApiClient() => RegisterTasksApiClient([]);

    private void RegisterTasksApiClient(IReadOnlyList<TaskDto> taskLists)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(taskLists))) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new TasksApiClient(httpClient));
    }

    private static TaskDto TaskList(string title)
        => new(
            Guid.NewGuid(), title, [], IsCompleted: false, IsGroup: false, IsPrivate: false, EncryptedContent: null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);

    private void RegisterEmptyCalendarApiClient()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(Array.Empty<CalendarEventDto>()))) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new CalendarApiClient(httpClient));
    }

    private static HttpResponseMessage JsonResponse<TItem>(IReadOnlyList<TItem> items)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(items) };
}
