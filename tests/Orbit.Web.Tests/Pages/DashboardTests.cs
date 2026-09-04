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
using Orbit.Contracts.Inventories;
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
        RegisterEmptyInventoryApiClient();
        RegisterDashboardPins();
        RegisterDashboardCardPreferences();
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

    /// <summary>
    /// What the reader has put away lives in localStorage too (see DashboardCardPreferences) - stubbed as
    /// "nothing hidden" unless a test says otherwise, so the rest see the whole page.
    /// </summary>
    private void RegisterDashboardCardPreferences(
        IReadOnlyDictionary<string, string>? filters = null, params string[] hidden)
    {
        var module = JSInterop.SetupModule("./js/dashboardCards.js");
        module.Setup<string[]>("getHiddenCards").SetResult(hidden);
        module.SetupVoid("setHiddenCards", _ => true);
        module.Setup<Dictionary<string, string>>("getCardFilters")
            .SetResult(filters is null ? [] : new Dictionary<string, string>(filters));
        module.SetupVoid("setCardFilters", _ => true);
        Services.AddScoped<DashboardCardPreferences>();
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
    public void Nobody_is_told_about_chat_requests_that_are_not_there()
    {
        RegisterChatApiClient([new ContactDto(
            Guid.NewGuid(), "anna", "Anna Kowalska", "anna@example.com", "public-key", DateTimeOffset.UtcNow,
            RequiresApprovalFromCurrentUser: false, IsPendingApprovalFromOtherParty: false)]);

        var cut = RenderComponent<Dashboard>();

        // A standing "0 new chat requests" is not news.
        Assert.DoesNotContain("new chat requests", cut.Markup);
    }

    [Fact]
    public void Somebody_waiting_to_be_answered_is_counted()
    {
        RegisterChatApiClient([new ContactDto(
            Guid.NewGuid(), "bartek", "Bartek Nowak", "bartek@example.com", "public-key", DateTimeOffset.UtcNow,
            RequiresApprovalFromCurrentUser: true, IsPendingApprovalFromOtherParty: false)]);

        var cut = RenderComponent<Dashboard>();

        Assert.Contains("new chat requests", cut.Markup);
    }

    [Fact]
    public void An_account_without_chat_is_not_shown_requests_it_could_not_answer()
    {
        Services.Remove(Services.Single(service => service.ServiceType == typeof(UserPermissionState)));
        RegisterPermissions(ApplicationPermission.Contacts);
        RegisterChatApiClient([new ContactDto(
            Guid.NewGuid(), "bartek", "Bartek Nowak", "bartek@example.com", "public-key", DateTimeOffset.UtcNow,
            RequiresApprovalFromCurrentUser: true, IsPendingApprovalFromOtherParty: false)]);

        var cut = RenderComponent<Dashboard>();

        // Approving one is the only thing to do with it, and that is exactly what this account cannot do.
        Assert.DoesNotContain("new chat requests", cut.Markup);
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
        // the list's own settings is a deliberate trip to the editor from there. "/tasks/{id}" is the
        // checklist; the editor is at "/tasks/{id}/edit".
        Assert.EndsWith($"/tasks/{taskList.Id}", Services.GetRequiredService<NavigationManager>().Uri);
    }

    private static ChatGroupDto Group(string name, int memberCount, string ownRole = "Member")
        => new(
            Guid.NewGuid(), name, Guid.NewGuid(), DateTimeOffset.UtcNow, ownRole,
            Enumerable.Range(0, memberCount)
                .Select(_ => new ChatGroupMemberDto(Guid.NewGuid(), "Member", DateTimeOffset.UtcNow))
                .ToList());

    [Fact]
    public void The_menu_offers_every_part_of_the_page()
    {
        RegisterChatApiClient([Contact("Anna Kowalska")]);
        var cut = RenderComponent<Dashboard>();

        OpenTheMenu(cut);

        Assert.Equal(
            ["Today", "Notes", "Tasks", "Upcoming", "Inventory", "Groups", "Shared with you", "Recent chats", "Contacts"],
            MenuEntries(cut).Select(entry => entry.TextContent.Trim()));
    }

    [Fact]
    public void A_part_the_reader_put_away_is_not_drawn()
    {
        RegisterDashboardCardPreferences(null, "chats");
        RegisterChatApiClient([Contact("Anna Kowalska")]);

        var cut = RenderComponent<Dashboard>();

        Assert.DoesNotContain(cut.FindAll(".item-card"), card => card.QuerySelector(".item-card-name")!.TextContent == "Recent chats");
        Assert.Contains("Anna Kowalska", FindColumn(cut, "Contacts").TextContent);
    }

    [Fact]
    public void Unticking_a_part_in_the_menu_takes_it_off_the_page()
    {
        RegisterChatApiClient([Contact("Anna Kowalska")]);
        var cut = RenderComponent<Dashboard>();
        OpenTheMenu(cut);

        MenuEntries(cut).Single(entry => entry.TextContent.Contains("Recent chats"))
            .QuerySelector("input")!.Change(false);

        Assert.DoesNotContain(cut.FindAll(".item-card"), card => card.QuerySelector(".item-card-name")!.TextContent == "Recent chats");
    }

    [Fact]
    public void The_menu_stays_open_while_several_parts_are_changed()
    {
        RegisterChatApiClient([Contact("Anna Kowalska")]);
        var cut = RenderComponent<Dashboard>();
        OpenTheMenu(cut);

        // The click a real browser sends reaches the menu itself, which closes on one when its entries
        // are actions. Closing after each tick here would make changing two a chore.
        MenuEntries(cut)[0].Click();

        Assert.NotEmpty(MenuEntries(cut));
    }

    [Fact]
    public void Ticking_a_part_back_on_puts_it_back()
    {
        RegisterDashboardCardPreferences(null, "chats");
        RegisterChatApiClient([Contact("Anna Kowalska")]);
        var cut = RenderComponent<Dashboard>();
        OpenTheMenu(cut);

        MenuEntries(cut).Single(entry => entry.TextContent.Contains("Recent chats"))
            .QuerySelector("input")!.Change(true);

        Assert.Contains("Anna Kowalska", FindColumn(cut, "Recent chats").TextContent);
    }

    [Fact]
    public void A_page_with_everything_put_away_says_so()
    {
        // Otherwise it reads as a dashboard that failed to load, with nothing saying where its contents went.
        RegisterDashboardCardPreferences(
            null, "today", "notes", "tasks", "upcoming", "inventories", "groups", "locations", "chats", "contacts");
        RegisterChatApiClient([Contact("Anna Kowalska")]);

        var cut = RenderComponent<Dashboard>();

        Assert.Empty(cut.FindAll(".item-card"));
        Assert.Contains("Everything here is hidden", cut.Markup);
    }

    [Fact]
    public void A_card_shows_everything_until_it_is_told_otherwise()
    {
        RegisterNotesApiClient([Note("Shopping", "High"), Note("Ideas", "Low", isPinned: true)]);
        RegisterChatApiClient([]);

        var cut = RenderComponent<Dashboard>();

        Assert.Equal(["Shopping", "Ideas"], RowTitlesIn(cut, "Notes"));
    }

    [Fact]
    public void A_card_filtered_to_one_priority_shows_only_that()
    {
        RegisterDashboardCardPreferences(new Dictionary<string, string> { ["notes"] = "HighPriority" });
        RegisterNotesApiClient([Note("Shopping", "High"), Note("Ideas", "Low")]);
        RegisterChatApiClient([]);

        var cut = RenderComponent<Dashboard>();

        Assert.Equal(["Shopping"], RowTitlesIn(cut, "Notes"));
    }

    [Fact]
    public void A_card_filtered_to_what_is_pinned_shows_only_that()
    {
        RegisterDashboardCardPreferences(new Dictionary<string, string> { ["notes"] = "Pinned" });
        RegisterNotesApiClient([Note("Shopping", "High"), Note("Ideas", "Low", isPinned: true)]);
        RegisterChatApiClient([]);

        var cut = RenderComponent<Dashboard>();

        Assert.Equal(["Ideas"], RowTitlesIn(cut, "Notes"));
    }

    [Fact]
    public void The_count_beside_a_card_counts_what_the_card_is_showing()
    {
        // Otherwise a filtered card says three and shows one, which reads as a card that lost something.
        RegisterDashboardCardPreferences(new Dictionary<string, string> { ["notes"] = "HighPriority" });
        RegisterNotesApiClient([Note("Shopping", "High"), Note("Ideas", "Low"), Note("Recipes", "Normal")]);
        RegisterChatApiClient([]);

        var cut = RenderComponent<Dashboard>();

        Assert.Equal("1", FindColumn(cut, "Notes").QuerySelector(".card-count")!.TextContent.Trim());
    }

    [Fact]
    public void Choosing_a_filter_from_the_cards_own_menu_applies_it()
    {
        RegisterNotesApiClient([Note("Shopping", "High"), Note("Ideas", "Low")]);
        RegisterChatApiClient([]);
        var cut = RenderComponent<Dashboard>();

        FindColumn(cut, "Notes").QuerySelector(".overflow-menu-trigger")!.Click();
        cut.FindAll(".overflow-menu-dropdown button").First(entry => entry.TextContent.Contains("High")).Click();

        Assert.Equal(["Shopping"], RowTitlesIn(cut, "Notes"));
    }

    [Fact]
    public void A_card_whose_items_cannot_be_pinned_does_not_offer_pinned()
    {
        // An event has a priority but nothing to pin it to.
        RegisterCalendarApiClient([Event("Dentist", DateTimeOffset.UtcNow.AddHours(2))]);
        RegisterChatApiClient([]);
        var cut = RenderComponent<Dashboard>();

        FindColumn(cut, "Upcoming").QuerySelector(".overflow-menu-trigger")!.Click();

        // The tick beside the chosen one is part of the row, so compare what each row says after it.
        var entries = cut.FindAll(".overflow-menu-dropdown button")
            .Select(entry => entry.TextContent.Replace("✓", "").Trim());
        Assert.Equal(["All", "High", "Normal", "Low"], entries);
    }

    private static IReadOnlyList<string> RowTitlesIn(IRenderedComponent<Dashboard> cut, string heading)
        => [.. FindColumn(cut, heading).QuerySelectorAll(".row-title").Select(row => row.TextContent.Trim())];

    private static NoteDto Note(string title, string priority, bool isPinned = false)
        => new(
            Guid.NewGuid(), title, [], IsPrivate: false, EncryptedContent: null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null,
            IsPinned: isPinned, Priority: priority);

    private static void OpenTheMenu(IRenderedComponent<Dashboard> cut)
        => cut.Find(".page-header-actions .overflow-menu-trigger").Click();

    private static IReadOnlyList<IElement> MenuEntries(IRenderedComponent<Dashboard> cut)
        => [.. cut.FindAll(".overflow-menu-dropdown label")];


    [Fact]
    public void A_contact_with_a_message_waiting_is_marked_on_the_recent_chats_card()
    {
        // The first thing a visit looks at. A card that knows a message is waiting and does not say so
        // reads as nobody waiting.
        RegisterChatApiClient([Contact("Anna Kowalska", unread: 2), Contact("Bartek Nowak")]);

        var cut = RenderComponent<Dashboard>();

        var chats = FindColumn(cut, "Recent chats");
        Assert.Equal("2", chats.QuerySelector(".notif-badge")!.TextContent);
        Assert.Single(chats.QuerySelectorAll(".notif-badge"));
    }

    [Fact]
    public void Nobody_waiting_leaves_the_recent_chats_card_unmarked()
    {
        RegisterChatApiClient([Contact("Anna Kowalska"), Contact("Bartek Nowak")]);

        var cut = RenderComponent<Dashboard>();

        Assert.Empty(FindColumn(cut, "Recent chats").QuerySelectorAll(".notif-badge"));
    }

    /// <summary>The card's own pulsing edge, the same "something happened here" every other card in
    /// Orbit carries - not only the badge on the one row that has news.</summary>
    [Fact]
    public void A_message_waiting_pulses_the_recent_chats_cards_own_edge()
    {
        RegisterChatApiClient([Contact("Anna Kowalska", unread: 2), Contact("Bartek Nowak")]);

        var cut = RenderComponent<Dashboard>();

        Assert.Contains("item-card-unseen", FindColumn(cut, "Recent chats").ClassName);
    }

    [Fact]
    public void Nobody_waiting_leaves_the_recent_chats_cards_edge_alone()
    {
        RegisterChatApiClient([Contact("Anna Kowalska"), Contact("Bartek Nowak")]);

        var cut = RenderComponent<Dashboard>();

        Assert.DoesNotContain("item-card-unseen", FindColumn(cut, "Recent chats").ClassName);
    }

    private static ContactDto Contact(string displayName, int unread = 0)
        => new(
            Guid.NewGuid(), displayName.ToLowerInvariant(), displayName, $"{displayName}@example.com", "public-key",
            DateTimeOffset.UtcNow, RequiresApprovalFromCurrentUser: false, IsPendingApprovalFromOtherParty: false,
            unread);

    private static IElement FindColumn(IRenderedComponent<Dashboard> cut, string heading)
        => cut.FindAll(".item-card").Single(column => column.QuerySelector(".item-card-name")!.TextContent == heading);

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

    private static TaskDto TaskList(string title, params TaskItemDto[] items) => TaskList(title, "Normal", items);

    private static TaskDto TaskList(string title, string priority, params TaskItemDto[] items)
        => new(
            Guid.NewGuid(), title, items, IsCompleted: false, IsGroup: false, IsPrivate: false, EncryptedContent: null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null,
            Priority: priority);

    private void RegisterEmptyCalendarApiClient()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(Array.Empty<CalendarEventDto>()))) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new CalendarApiClient(httpClient));
    }

    /// <summary>
    /// The shelves were the one part of Orbit this page never mentioned, so the only way to "have we
    /// run out" was through the side navigation.
    /// </summary>
    [Fact]
    public void An_inventory_is_shown_on_the_dashboard_and_opens_what_is_on_it()
    {
        RegisterInventoryApiClient([Inventory("Pantry")]);

        var cut = RenderComponent<Web.Pages.Dashboard>();

        Assert.Contains("Pantry", cut.Markup);
        var row = cut.FindAll("button.list-row-button").Single(button => button.TextContent.Contains("Pantry"));
        row.Click();

        Assert.Contains("/inventory/", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void No_inventories_means_no_inventory_card()
    {
        var cut = RenderComponent<Web.Pages.Dashboard>();

        Assert.DoesNotContain(
            cut.FindAll("button.item-card-name"), name => name.TextContent.Trim() == "Inventory");
    }

    private void RegisterEmptyInventoryApiClient() => RegisterInventoryApiClient([]);

    private void RegisterInventoryApiClient(IReadOnlyList<InventoryDto> inventories)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(inventories))) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new InventoryApiClient(httpClient));
    }

    private static InventoryDto Inventory(string name)
        => new(
            Guid.NewGuid(), name, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit",
            LockedByUserName: null, OriginalOwnerUserId: null);

    private static HttpResponseMessage JsonResponse<TItem>(IReadOnlyList<TItem> items)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(items) };
    [Fact]
    public void An_event_that_has_already_finished_is_not_upcoming()
    {
        // A card headed "Upcoming" that opens with last month is a card nobody reads twice.
        RegisterChatApiClient([]);
        RegisterCalendarApiClient([Event("Last month", DateTimeOffset.UtcNow.AddDays(-30))]);

        var cut = RenderComponent<Dashboard>();

        Assert.DoesNotContain("Upcoming", cut.Markup);
        Assert.DoesNotContain("Last month", cut.Markup);
    }

    [Fact]
    public void An_event_still_ahead_is()
    {
        RegisterChatApiClient([]);
        RegisterCalendarApiClient([
            Event("Last month", DateTimeOffset.UtcNow.AddDays(-30)),
            Event("Next week", DateTimeOffset.UtcNow.AddDays(7))]);

        var cut = RenderComponent<Dashboard>();

        Assert.Contains("Next week", cut.Markup);
        Assert.DoesNotContain("Last month", cut.Markup);
    }

    [Fact]
    public void An_event_running_right_now_has_not_been_and_gone()
    {
        RegisterChatApiClient([]);
        RegisterCalendarApiClient([Event("Happening now", DateTimeOffset.UtcNow.AddMinutes(-30), lengthHours: 2)]);

        var cut = RenderComponent<Dashboard>();

        Assert.Contains("Happening now", cut.Markup);
    }

    private void RegisterCalendarApiClient(IReadOnlyList<CalendarEventDto> events)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(events)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new CalendarApiClient(httpClient));
    }

    private static CalendarEventDto Event(
        string title, DateTimeOffset startUtc, int lengthHours = 1, RecurrenceDto? recurrence = null,
        string priority = "Normal")
        => new(
            Guid.NewGuid(),
            new CalendarEventDetailsDto(
                title, Description: null, Location: null, Color: null, startUtc, startUtc.AddHours(lengthHours),
                IsAllDay: false, Recurrence: recurrence, Guests: [], ReminderMinutesBeforeStart: [], ReminderNotificationChannel: "None", Priority: priority),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);

    [Fact]
    public void A_repeating_event_is_upcoming_at_its_next_repeat()
    {
        // Stored back in the spring, still on the calendar every week - and missing from this card until
        // it started expanding recurrences the way the calendar always has.
        RegisterChatApiClient([]);
        RegisterEmptyNotesApiClient();
        RegisterTasksApiClient([]);
        RegisterCalendarApiClient([
            Event("Standup", DateTimeOffset.UtcNow.AddDays(-60), recurrence: new RecurrenceDto("Weekly", 1, null))]);

        var cut = RenderComponent<Dashboard>();

        Assert.Contains("Standup", FindColumn(cut, "Upcoming").TextContent);
    }

    [Fact]
    public void A_deadline_is_upcoming_too_and_says_which_list_it_is_on()
    {
        RegisterChatApiClient([]);
        RegisterEmptyNotesApiClient();
        RegisterEmptyCalendarApiClient();
        RegisterTasksApiClient([TaskList("Shopping", DueItem("Milk", DateTimeOffset.UtcNow.AddDays(1)))]);

        var cut = RenderComponent<Dashboard>();

        // The calendar shows deadlines beside events; a card headed "Upcoming" that left them out was
        // not showing what is coming up.
        Assert.Contains("Shopping: Milk", FindColumn(cut, "Upcoming").TextContent);
    }

    [Fact]
    public void A_deadline_already_ticked_off_is_not_upcoming()
    {
        RegisterChatApiClient([]);
        RegisterEmptyNotesApiClient();
        RegisterEmptyCalendarApiClient();
        RegisterTasksApiClient([TaskList("Shopping", DueItem("Milk", DateTimeOffset.UtcNow.AddDays(1), isCompleted: true))]);

        var cut = RenderComponent<Dashboard>();

        Assert.DoesNotContain(cut.FindAll(".item-card"), card => card.QuerySelector(".item-card-name")!.TextContent == "Upcoming");
    }

    [Fact]
    public void A_row_that_matters_more_than_the_rest_says_so()
    {
        RegisterChatApiClient([]);
        RegisterEmptyNotesApiClient();
        RegisterEmptyCalendarApiClient();
        RegisterTasksApiClient([TaskList("Urgent", priority: "High"), TaskList("Ordinary")]);

        var cut = RenderComponent<Dashboard>();

        // Normal is the default and says nothing, so it is drawn as nothing rather than on every line.
        var badge = Assert.Single(FindColumn(cut, "Tasks").QuerySelectorAll(".card-badge"));
        Assert.Equal("High", badge.TextContent);
    }

    [Fact]
    public void Todays_summary_opens_the_calendar()
    {
        RegisterChatApiClient([]);
        RegisterEmptyNotesApiClient();
        RegisterEmptyCalendarApiClient();
        RegisterTasksApiClient([TaskList("Errands")]);
        var cut = RenderComponent<Dashboard>();

        cut.Find(".today-strip").Click();

        // It is a summary of a day, and the page that shows a day is the calendar.
        Assert.EndsWith("/calendar", Services.GetRequiredService<NavigationManager>().Uri);
    }

    private static TaskItemDto DueItem(string description, DateTimeOffset dueDateUtc, bool isCompleted = false)
        => new(
            Guid.NewGuid(), description, dueDateUtc, isCompleted, LinkedTaskListId: null,
            OverdueNotificationChannel: "None", RemindDaily: false,
            DailyReminderNotificationChannel: "None", DailyReminderTimeOfDay: new TimeOnly(9, 0));

    /// <summary>
    /// A filter that matches nothing must not take the card away with it.
    ///
    /// The reported bug: choosing "High" on Upcoming with nothing high coming up removed the whole
    /// card - and the filter menu lives in that card's header, so the choice could not be undone from
    /// the page that made it. Reloading did not help either, because the filter is remembered. The card
    /// is gated on whether anything is coming up at all, which is a question about the account rather
    /// than about the filter.
    /// </summary>
    [Fact]
    public void A_filter_that_matches_nothing_leaves_the_card_and_its_filter_reachable()
    {
        RegisterChatApiClient([]);
        RegisterDashboardCardPreferences(new Dictionary<string, string> { ["upcoming"] = "HighPriority" });
        RegisterCalendarApiClient([EventSoon("Dentist", "Normal")]);

        var cut = RenderComponent<Dashboard>();

        var card = FindColumn(cut, "Upcoming");
        Assert.NotNull(card.QuerySelector(".card-filter-trigger, .overflow-menu-trigger"));
        Assert.Contains("Nothing here matches the filter", card.TextContent);
        Assert.Empty(card.QuerySelectorAll(".list-row"));
    }

    /// <summary>The same card with a filter that does match still shows what it matched.</summary>
    [Fact]
    public void A_filter_that_matches_something_still_shows_it()
    {
        RegisterChatApiClient([]);
        RegisterDashboardCardPreferences(new Dictionary<string, string> { ["upcoming"] = "HighPriority" });
        RegisterCalendarApiClient([EventSoon("Dentist", "High")]);

        var cut = RenderComponent<Dashboard>();

        var card = FindColumn(cut, "Upcoming");
        Assert.Single(card.QuerySelectorAll(".list-row"));
        Assert.DoesNotContain("Nothing here matches the filter", card.TextContent);
    }

    /// <summary>
    /// An account with nothing coming up at all still has no Upcoming card - the fix is about the
    /// filter hiding things, not about showing an empty card to somebody with an empty calendar.
    /// </summary>
    [Fact]
    public void An_account_with_nothing_coming_up_has_no_card_at_all()
    {
        RegisterChatApiClient([]);
        RegisterDashboardCardPreferences(new Dictionary<string, string> { ["upcoming"] = "HighPriority" });

        var cut = RenderComponent<Dashboard>();

        Assert.DoesNotContain(cut.FindAll(".item-card"), card => card.QuerySelector(".item-card-name")!.TextContent == "Upcoming");
    }

    /// <summary>Notes kept its card when filtered to nothing, but rendered a silent void; now it says why.</summary>
    [Fact]
    public void A_notes_card_filtered_to_nothing_says_so_rather_than_showing_a_blank()
    {
        RegisterChatApiClient([]);
        RegisterDashboardCardPreferences(new Dictionary<string, string> { ["notes"] = "HighPriority" });
        RegisterNotesApiClient([Note("Shopping", "Normal")]);

        var cut = RenderComponent<Dashboard>();

        Assert.Contains("Nothing here matches the filter", FindColumn(cut, "Notes").TextContent);
    }

    private static CalendarEventDto EventSoon(string title, string priority)
        => new(
            Guid.NewGuid(),
            new CalendarEventDetailsDto(
                title, null, null, "#ffffff",
                DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
                IsAllDay: false, null, [], [], "None", Priority: priority),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);
}
