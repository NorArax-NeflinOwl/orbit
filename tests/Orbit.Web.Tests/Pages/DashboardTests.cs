using System.Net;
using System.Net.Http.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Chat;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Tasks;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

public sealed class DashboardTests : TestContext
{
    public DashboardTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        // Notes/task lists/events aren't what these tests exercise - each is stubbed to an empty list so
        // the dashboard finishes loading without depending on unrelated fixture data.
        RegisterEmptyNotesApiClient();
        RegisterEmptyTasksApiClient();
        RegisterEmptyCalendarApiClient();
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
    public void Clicking_a_group_opens_it()
    {
        var group = Group("Weekend trip", memberCount: 3);
        RegisterChatApiClient([], [group]);
        var cut = RenderComponent<Dashboard>();

        FindColumn(cut, "Groups").QuerySelector("button")!.Click();

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

        FindColumn(cut, "Tasks").QuerySelector("button")!.Click();

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

    private void RegisterEmptyNotesApiClient()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(Array.Empty<NoteDto>()))) { BaseAddress = new Uri("https://example.test/") };
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
