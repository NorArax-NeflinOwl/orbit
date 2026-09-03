using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Inventory;
using Orbit.Contracts.Tasks;
using Orbit.Contracts.Users;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Orbit.Web.Tests;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// Covers the shallow editing level: a whole list as tickable rows, a group list rendered together with
/// the lists it gathers, and which rows are deliberately not tickable.
/// </summary>
public sealed class TaskListChecklistTests : OrbitTestContext
{
    private readonly List<HttpRequestMessage> _requests = [];
    private readonly List<string> _requestBodies = [];

    /// <summary>
    /// Whether the account the gate sees qualifies for the Google links. Set before rendering; the gate
    /// asks once and caches, and xUnit builds a fresh instance of this class per test.
    /// </summary>
    private bool _googleExtrasAvailable;

    public TaskListChecklistTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        RegisterGoogleIntegrationAccess();
        RegisterChecklistViewPreference();
        RegisterInventoryApiClient();
    }

    /// <summary>
    /// The same marks the cards on /tasks carry. Somebody working through a list should see what an
    /// entry is about where the work is actually done, not only on the page they came from.
    /// </summary>
    [Fact]
    public void A_row_says_what_it_is_filed_under()
    {
        var taskList = TaskList("Errands", Item("Buy milk") with { Categories = ["shopping", "weekly"] });
        RegisterTasksApiClient([taskList]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, taskList.Id));

        Assert.Equal(
            ["shopping", "weekly"],
            cut.FindAll(".check-row .row-category").Select(category => category.TextContent.Trim()));
    }

    [Fact]
    public void Every_item_on_the_list_is_rendered_as_a_tickable_row()
    {
        var taskList = TaskList("Errands", Item("Buy milk"), Item("Post parcel", isCompleted: true));
        RegisterTasksApiClient([taskList]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, taskList.Id));

        var rows = cut.FindAll(".check-row").ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Contains("Buy milk", rows[0].TextContent);
        Assert.Contains("completed", rows[1].ClassName);
    }

    [Fact]
    public void A_plain_list_shows_only_itself_even_when_other_lists_exist()
    {
        var taskList = TaskList("Errands", Item("Buy milk"));
        var unrelated = TaskList("Work", Item("Write report"));
        RegisterTasksApiClient([taskList, unrelated]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, taskList.Id));

        Assert.Single(cut.FindAll(".checklist-card"));
        Assert.DoesNotContain("Write report", cut.Markup);
    }

    [Fact]
    public void A_group_list_also_renders_the_lists_its_items_link_to()
    {
        var kitchen = TaskList("Kitchen", Item("Paint walls"));
        var bathroom = TaskList("Bathroom", Item("Replace tap"));
        var group = TaskList(
            "Renovation",
            Item("Kitchen done", linkedTaskListId: kitchen.Id),
            Item("Bathroom done", linkedTaskListId: bathroom.Id));
        group = group with { IsGroup = true };
        RegisterTasksApiClient([group, kitchen, bathroom]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, group.Id));

        // The group itself plus one card per member, each member's own items tickable in place.
        Assert.Equal(3, cut.FindAll(".checklist-card").Count);
        Assert.Contains("Paint walls", cut.Markup);
        Assert.Contains("Replace tap", cut.Markup);
    }

    [Fact]
    public void An_unticked_group_list_does_not_expand_its_members()
    {
        // Same shape as the group test, minus the flag - the links alone must not pull other lists in.
        var kitchen = TaskList("Kitchen", Item("Paint walls"));
        var plain = TaskList("Renovation", Item("Kitchen done", linkedTaskListId: kitchen.Id));
        RegisterTasksApiClient([plain, kitchen]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, plain.Id));

        Assert.Single(cut.FindAll(".checklist-card"));
        Assert.DoesNotContain("Paint walls", cut.Markup);
    }

    /// <summary>
    /// Its completion is derived from the list it points at (see LinkedTaskCompletionResolver), so
    /// setting it here would be a change the next reload silently undoes. The press is still worth
    /// taking: it says where the answer is, and offers to go there.
    /// </summary>
    [Fact]
    public void Ticking_an_item_that_follows_another_list_offers_that_list_instead()
    {
        var kitchen = TaskList("Kitchen", Item("Paint walls"));
        var group = TaskList("Renovation", Item("Kitchen done", linkedTaskListId: kitchen.Id)) with { IsGroup = true };
        RegisterTasksApiClient([group, kitchen]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, group.Id));
        Assert.Contains("follows Kitchen", cut.Markup);

        cut.FindAll(".check-row input[type=checkbox]").First().Click();

        Assert.Contains("This is done when Kitchen is.", cut.Markup);
        // And nothing was saved: the box says the same thing it did before.
        Assert.DoesNotContain(_requests, request => request.Method == HttpMethod.Put);

        cut.FindAll(".check-row-asks button").First(button => button.TextContent.Trim() == "Yes").Click();

        Assert.EndsWith($"/tasks/{kitchen.Id}", navigationManager.Uri);
    }

    [Fact]
    public void A_read_only_share_renders_every_item_as_look_but_do_not_touch()
    {
        var taskList = TaskList("Errands", Item("Buy milk")) with { AccessLevel = "ReadOnly", IsShared = true };
        RegisterTasksApiClient([taskList]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, taskList.Id));

        Assert.True(cut.Find(".check-row input[type=checkbox]").HasAttribute("disabled"));
    }

    /// <summary>
    /// Ticking a box must not change what the other entries *are*.
    ///
    /// The endpoint replaces the list wholesale, so every field of every item has to ride along - and
    /// four of them did not. Kind, Location and the two links fell back to their defaults, so ticking
    /// anything on a list turned its inventory errands and its appointments into plain checklist lines
    /// and cut them loose from the shelf item and the event they were about. On the Restock supplies
    /// list that is what made entries disappear as soon as they were ticked: the link the restock
    /// reconciliation recognises them by was gone.
    /// </summary>
    [Fact]
    public void Ticking_an_item_leaves_the_other_entries_as_the_kind_of_thing_they_were()
    {
        var shelfItemId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var taskList = TaskList(
            "Zakupy",
            Item("Buy milk"),
            Item("Restock: Parówki", kind: "Inventory") with { LinkedInventoryItemId = shelfItemId },
            Item("Dentist", kind: "Calendar") with { Location = "Długa 4", LinkedCalendarEventId = eventId });
        RegisterTasksApiClient([taskList]);
        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, taskList.Id));

        cut.FindAll(".check-row input[type=checkbox]").ToArray()[0].Change(true);

        var update = _requests.Single(request => request.Method == HttpMethod.Put);
        var items = JsonDocument.Parse(_requestBodies[_requests.IndexOf(update)]).RootElement.GetProperty("items");

        Assert.Equal("Inventory", items[1].GetProperty("kind").GetString());
        Assert.Equal(shelfItemId, items[1].GetProperty("linkedInventoryItemId").GetGuid());
        Assert.Equal("Calendar", items[2].GetProperty("kind").GetString());
        Assert.Equal("Długa 4", items[2].GetProperty("location").GetString());
        Assert.Equal(eventId, items[2].GetProperty("linkedCalendarEventId").GetGuid());
    }

    [Fact]
    public void Ticking_an_item_saves_the_whole_list_back_with_only_that_item_changed()
    {
        var taskList = TaskList("Errands", Item("Buy milk"), Item("Post parcel"));
        RegisterTasksApiClient([taskList]);
        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, taskList.Id));

        cut.FindAll(".check-row input[type=checkbox]").ToArray()[1].Change(true);

        var update = _requests.Single(request => request.Method == HttpMethod.Put);
        Assert.Equal($"https://example.test/api/tasks/{taskList.Id}", update.RequestUri!.ToString());
        var body = _requestBodies[_requests.IndexOf(update)];
        // The untouched item keeps its state, and the list's own title and grouping ride along - the
        // endpoint replaces the list wholesale, so anything left out would be erased.
        var items = JsonDocument.Parse(body).RootElement.GetProperty("items");
        Assert.Equal("Buy milk", items[0].GetProperty("description").GetString());
        Assert.False(items[0].GetProperty("isCompleted").GetBoolean());
        Assert.Equal("Post parcel", items[1].GetProperty("description").GetString());
        Assert.True(items[1].GetProperty("isCompleted").GetBoolean());
        Assert.Equal("Errands", JsonDocument.Parse(body).RootElement.GetProperty("title").GetString());

        // And each entry goes back under the id it already had. Without this the server minted new ones
        // on every save, cutting loose everything that points at an entry by id - an inventory item's
        // restock task most visibly, which then grew a duplicate.
        Assert.Equal(taskList.Items[0].Id, items[0].GetProperty("id").GetGuid());
        Assert.Equal(taskList.Items[1].Id, items[1].GetProperty("id").GetGuid());
    }

    [Fact]
    public void Ticking_an_item_on_a_member_list_saves_that_list_rather_than_the_group()
    {
        var kitchen = TaskList("Kitchen", Item("Paint walls"));
        var group = TaskList("Renovation", Item("Kitchen done", linkedTaskListId: kitchen.Id)) with { IsGroup = true };
        RegisterTasksApiClient([group, kitchen]);
        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, group.Id));

        // Index 1: the group's own linked row is index 0 and is disabled.
        cut.FindAll(".check-row input[type=checkbox]").ToArray()[1].Change(true);

        var update = _requests.Single(request => request.Method == HttpMethod.Put);
        Assert.Equal($"https://example.test/api/tasks/{kitchen.Id}", update.RequestUri!.ToString());
    }

    [Fact]
    public void A_list_that_no_longer_exists_says_so_instead_of_rendering_an_empty_checklist()
    {
        RegisterTasksApiClient([]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, Guid.NewGuid()));

        Assert.Contains("no longer exists", cut.Markup);
        Assert.Empty(cut.FindAll(".checklist-card"));
    }


    // A_due_item_offers_to_go_into_google_calendar lived here. The checklist no longer offers to put
    // a deadline into Google Calendar - the row is for reading and ticking off, and the link was a
    // third control competing with the two that matter. GoogleCalendarEventLink itself is unchanged
    // and still covered by its own tests; only this page stopped using it.


    [Fact]
    public void An_account_without_the_google_extras_is_offered_none()
    {
        // The default for these tests - see RegisterGoogleIntegrationAccess.
        var taskList = TaskList("Admin", Item("File the return", dueDateUtc: new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero)));
        RegisterTasksApiClient([taskList]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, taskList.Id));

        Assert.Empty(cut.FindAll("a.google-link"));
    }

    [Fact]
    public void An_item_with_no_due_date_has_nothing_to_put_in_a_calendar()
    {
        _googleExtrasAvailable = true;
        var taskList = TaskList("Admin", Item("Someday"));
        RegisterTasksApiClient([taskList]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, taskList.Id));

        Assert.Empty(cut.FindAll("a.google-link"));
    }

    /// <summary>
    /// The pages inject this to decide whether to offer the Google links. Registered over a stubbed
    /// account rather than a live one: a real HttpClient here would spend wall-clock time on a DNS
    /// lookup bUnit's synchronous render doesn't wait out.
    /// </summary>
    private void RegisterGoogleIntegrationAccess()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new AccountDto(
                Guid.NewGuid(), "owner@example.com", "owner", "Owner",
                IsEmailVerified: _googleExtrasAvailable, HasPassword: true, IsGoogleLinked: false))
        });
        Services.AddSingleton(new GoogleIntegrationAccess(
            new UsersApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }),
            // Never initialised, so the extras are on - which leaves the account above as the only
            // thing deciding, and it is the thing these tests are pointed at.
            new DevicePreferences(new StubJSRuntime()),
            NullLogger<GoogleIntegrationAccess>.Instance));
    }

    /// <param name="stockCheck">
    /// What the list costs against its warehouse, for the tests that are about that panel. Null means no
    /// warehouse is chosen for these fixtures, which is what the stock check answers 404 to - and the
    /// page reads as "no question to answer" rather than as a failure.
    /// </param>
    private void RegisterTasksApiClient(IReadOnlyList<TaskDto> taskLists, TaskListStockCheckDto? stockCheck = null)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            _requests.Add(request);
            _requestBodies.Add(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty);
            if (request.RequestUri!.AbsolutePath.EndsWith("/stock-check", StringComparison.Ordinal))
            {
                return stockCheck is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(stockCheck) };
            }

            return request.Method == HttpMethod.Put
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(taskLists) };
        });
        Services.AddSingleton(new TasksApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));
    }

    [Fact]
    public void The_list_is_read_in_its_own_order_until_another_one_is_asked_for()
    {
        var taskList = TaskList("Zakupy", Item("Ser"), Item("Bułki"), Item("Makaron"));
        RegisterTasksApiClient([taskList]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, taskList.Id));

        Assert.Equal(["Ser", "Bułki", "Makaron"], ItemTextsIn(cut));
    }

    [Fact]
    public void Choosing_A_to_Z_reads_the_list_alphabetically()
    {
        var taskList = TaskList("Zakupy", Item("Ser"), Item("Bułki"), Item("Makaron"));
        RegisterTasksApiClient([taskList]);
        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, taskList.Id));

        ChooseInMenu(cut, "A to Z");

        Assert.Equal(["Bułki", "Makaron", "Ser"], ItemTextsIn(cut));
    }

    [Fact]
    public void Left_to_do_first_puts_what_is_done_at_the_bottom()
    {
        var taskList = TaskList(
            "Zakupy", Item("Ser", isCompleted: true), Item("Bułki"), Item("Makaron", isCompleted: true), Item("Chleb"));
        RegisterTasksApiClient([taskList]);
        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, taskList.Id));

        ChooseInMenu(cut, "Left to do first");

        // What is left to do, alphabetically, then what is done, alphabetically.
        Assert.Equal(["Bułki", "Chleb", "Makaron", "Ser"], ItemTextsIn(cut));
    }

    [Fact]
    public void A_saved_order_is_what_the_list_opens_in()
    {
        var taskList = TaskList("Zakupy", Item("Ser"), Item("Bułki"));
        RegisterTasksApiClient([taskList]);
        RegisterChecklistViewPreference(new ChecklistViewPreference.SavedReading("tree", "alphabetical"));

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, taskList.Id));

        Assert.Equal(["Bułki", "Ser"], ItemTextsIn(cut));
        // Nothing has been changed since it was opened, so there is nothing to save.
        Assert.True(FindSaveViewButton(cut).HasAttribute("disabled"));
    }

    [Fact]
    public void Changing_the_order_is_something_to_save()
    {
        var taskList = TaskList("Zakupy", Item("Ser"), Item("Bułki"));
        RegisterTasksApiClient([taskList]);
        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, taskList.Id));

        ChooseInMenu(cut, "A to Z");

        Assert.False(FindSaveViewButton(cut).HasAttribute("disabled"));
    }

    [Fact]
    public void Alphabetical_runs_across_the_whole_tree_when_the_lists_are_flattened()
    {
        // Renovation -> Kitchen -> Tiling, read as one run of items: sorting list by list would look
        // random once the headings are gone.
        var tiling = TaskList("Tiling", Item("Grout"));
        var kitchen = TaskList("Kitchen", Item("Tiling done", linkedTaskListId: tiling.Id), Item("Hinge")) with { IsGroup = true };
        var renovation = TaskList("Renovation", Item("Kitchen done", linkedTaskListId: kitchen.Id), Item("Screw")) with { IsGroup = true };
        RegisterTasksApiClient([renovation, kitchen, tiling]);
        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, renovation.Id));

        ChooseInMenu(cut, "Show single items");
        ChooseInMenu(cut, "A to Z");

        Assert.Equal(["Grout", "Hinge", "Screw"], ItemTextsIn(cut));
    }

    /// <summary>What each tickable row says, without the list name and due date the row also carries.</summary>
    private static IReadOnlyList<string> ItemTextsIn(IRenderedComponent<TaskListChecklist> cut)
        => [.. cut.FindAll(".check-row .check-row-text").Select(row => row.TextContent.Trim())];

    private static AngleSharp.Dom.IElement FindSaveViewButton(IRenderedComponent<TaskListChecklist> cut)
    {
        OpenMenu(cut);
        return cut.FindAll(".page-header-actions .overflow-menu-dropdown .avatar-dropdown-item")
            .First(entry => entry.TextContent.Contains("view", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Opens the header menu everything but ticking now lives behind - the first menu on the page, since
    /// the stock-check panel below has one of its own.
    /// </summary>
    private static void OpenMenu(IRenderedComponent<TaskListChecklist> cut)
    {
        if (cut.FindAll(".page-header-actions .overflow-menu-dropdown").Count == 0)
        {
            cut.FindAll(".page-header-actions .overflow-menu-trigger").First().Click();
        }
    }

    /// <summary>Picks an entry out of the header menu by the words on it.</summary>
    private static void ChooseInMenu(IRenderedComponent<TaskListChecklist> cut, string label)
    {
        OpenMenu(cut);
        cut.FindAll(".page-header-actions .overflow-menu-dropdown .avatar-dropdown-item")
            .First(entry => entry.TextContent.Contains(label))
            .Click();
    }

    private static TaskDto TaskList(string title, params TaskItemDto[] items)
        => new(
            Guid.NewGuid(), title, items, IsCompleted: false, IsGroup: false, IsPrivate: false, EncryptedContent: null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);

    private static TaskItemDto Item(
        string description, bool isCompleted = false, Guid? linkedTaskListId = null, DateTimeOffset? dueDateUtc = null,
        string kind = "Checklist")
        => new(
            Guid.NewGuid(), description, dueDateUtc, isCompleted, linkedTaskListId,
            OverdueNotificationChannel: "None", RemindDaily: false,
            DailyReminderNotificationChannel: "None", DailyReminderTimeOfDay: default,
            Kind: kind);

    [Fact]
    public void A_group_shows_the_lists_below_its_own_members_too()
    {
        // Renovation -> Kitchen -> Tiling. Stopping at Kitchen would hide the work that is actually left.
        var tiling = TaskList("Tiling", Item("Grout"));
        var kitchen = TaskList("Kitchen", Item("Tiling done", linkedTaskListId: tiling.Id)) with { IsGroup = true };
        var renovation = TaskList("Renovation", Item("Kitchen done", linkedTaskListId: kitchen.Id)) with { IsGroup = true };
        RegisterTasksApiClient([renovation, kitchen, tiling]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, renovation.Id));

        Assert.Equal(["Kitchen", "Tiling"], cut.FindAll(".checklist-card .card-title").Select(card => card.TextContent.Trim()));
        Assert.Contains("Grout", cut.Markup);
    }

    [Fact]
    public void A_list_appears_directly_under_the_one_that_links_to_it()
    {
        // Depth-first: Kitchen's own subtree comes before Garden, not after every sibling.
        var tiling = TaskList("Tiling", Item("Grout"));
        var kitchen = TaskList("Kitchen", Item("Tiling done", linkedTaskListId: tiling.Id)) with { IsGroup = true };
        var garden = TaskList("Garden", Item("Mow"));
        var renovation = TaskList("Renovation",
            Item("Kitchen done", linkedTaskListId: kitchen.Id),
            Item("Garden done", linkedTaskListId: garden.Id)) with { IsGroup = true };
        RegisterTasksApiClient([renovation, kitchen, tiling, garden]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, renovation.Id));

        Assert.Equal(["Kitchen", "Tiling", "Garden"],
            cut.FindAll(".checklist-card .card-title").Select(card => card.TextContent.Trim()));
    }

    [Fact]
    public void How_deep_a_list_sits_is_written_on_its_card()
    {
        var tiling = TaskList("Tiling", Item("Grout"));
        var kitchen = TaskList("Kitchen", Item("Tiling done", linkedTaskListId: tiling.Id)) with { IsGroup = true };
        var renovation = TaskList("Renovation", Item("Kitchen done", linkedTaskListId: kitchen.Id)) with { IsGroup = true };
        RegisterTasksApiClient([renovation, kitchen, tiling]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, renovation.Id));

        // The indent is CSS's business; the depth it reads is this page's.
        var depths = cut.FindAll(".checklist-card").Select(card => card.GetAttribute("style")).ToArray();
        Assert.Contains("--checklist-depth: 0", depths[0]);
        Assert.Contains("--checklist-depth: 1", depths[1]);
        Assert.Contains("--checklist-depth: 2", depths[2]);
    }

    [Fact]
    public void A_list_that_links_back_to_an_ancestor_does_not_unfold_forever()
    {
        // Two lists pointing at each other. Without the guard this is a stack overflow, not a page.
        var firstId = Guid.NewGuid();
        var second = TaskList("Second", Item("Back to the first", linkedTaskListId: firstId)) with { IsGroup = true };
        var first = new TaskDto(
            firstId, "First", [Item("On to the second", linkedTaskListId: second.Id)], IsCompleted: false,
            IsGroup: true, IsPrivate: false, EncryptedContent: null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);
        RegisterTasksApiClient([first, second]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, first.Id));

        Assert.Equal(["Second"], cut.FindAll(".checklist-card .card-title").Select(card => card.TextContent.Trim()));
    }

    [Fact]
    public void A_list_linked_from_two_places_is_shown_once()
    {
        // The second copy would carry the same items and the same checkboxes, and ticking one would
        // leave the other looking untouched.
        var shared = TaskList("Shopping", Item("Milk"));
        var group = TaskList("Weekend",
            Item("Shopping done", linkedTaskListId: shared.Id),
            Item("Shopping again", linkedTaskListId: shared.Id)) with { IsGroup = true };
        RegisterTasksApiClient([group, shared]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, group.Id));

        Assert.Equal(["Shopping"], cut.FindAll(".checklist-card .card-title").Select(card => card.TextContent.Trim()));
    }

    [Fact]
    public void A_list_that_is_not_a_group_shows_only_itself()
    {
        var other = TaskList("Other", Item("Something"));
        var plain = TaskList("Errands", Item("Buy milk"));
        RegisterTasksApiClient([plain, other]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, plain.Id));

        Assert.Empty(cut.FindAll(".checklist-card .card-title"));
    }

    [Fact]
    public void A_group_with_nothing_nested_in_it_is_not_offered_a_flat_view()
    {
        // One row of plain members already reads as one page with headings - there is nothing to flatten.
        var kitchen = TaskList("Kitchen", Item("Tiles"));
        var group = TaskList("Renovation", Item("Kitchen done", linkedTaskListId: kitchen.Id)) with { IsGroup = true };
        RegisterTasksApiClient([group, kitchen]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, group.Id));

        OpenMenu(cut);

        Assert.DoesNotContain("Show single items", cut.Markup);
    }

    [Fact]
    public void A_tree_deeper_than_one_level_is_offered_a_flat_view()
    {
        var tree = ARenovationTree();
        RegisterTasksApiClient(tree);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, tree[0].Id));

        OpenMenu(cut);

        Assert.Contains("Show single items", cut.Markup);
    }

    [Fact]
    public void The_flat_view_shows_every_item_in_the_tree_and_no_headings()
    {
        var tree = ARenovationTree();
        RegisterTasksApiClient(tree);
        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, tree[0].Id));

        ChooseInMenu(cut, "Show single items");

        var rows = cut.FindAll(".check-row").Select(row => row.TextContent).ToList();
        Assert.Contains(rows, row => row.Contains("Grout"));
        Assert.Contains(rows, row => row.Contains("Mow"));
        // The rows that only point at another list are how the tree is held together, not work to tick.
        Assert.DoesNotContain(rows, row => row.Contains("Kitchen done"));
        Assert.Empty(cut.FindAll(".checklist-card .card-title"));
    }

    [Fact]
    public void The_stock_check_can_be_read_shortfalls_first()
    {
        var group = TaskList("Renovation", Item("Screw", kind: "Inventory")) with { IsGroup = true };
        RegisterTasksApiClient([group], new TaskListStockCheckDto(IsAchievable: false, [
            new StockRequirementDto("Anchor", 1, 1, 0),
            new StockRequirementDto("Screw", 2, 0, 2),
            new StockRequirementDto("Bolt", 1, 1, 0)]));
        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, group.Id));

        ChooseInStockCheckMenu(cut, "Short first");

        // The only row anybody has to act on, first - the rest are already answered.
        Assert.Equal(["Screw", "Anchor", "Bolt"], StockCheckNames(cut));
    }

    [Fact]
    public void Putting_the_stock_check_away_is_remembered_without_pressing_Save_view()
    {
        var group = TaskList("Renovation", Item("Screw", kind: "Inventory")) with { IsGroup = true };
        RegisterTasksApiClient([group], new TaskListStockCheckDto(IsAchievable: true, [
            new StockRequirementDto("Screw", 1, 1, 0)]));
        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, group.Id));

        // Folded by the card itself - the name and the chevron do it, so the menu no longer carries a
        // third way to say the same thing.
        cut.FindAll(".item-card .item-card-collapse").First().Click();

        Assert.Empty(cut.FindAll(".permissions-table"));
        // Nothing left over to save: a panel somebody puts away has already been answered about.
        Assert.True(FindSaveViewButton(cut).HasAttribute("disabled"));
    }

    /// <summary>The names in the stock-check table, in the order it lists them.</summary>
    private static IReadOnlyList<string> StockCheckNames(IRenderedComponent<TaskListChecklist> cut)
        => [.. cut.FindAll(".permissions-table tbody tr td:first-child").Select(cell => cell.TextContent.Trim())];

    /// <summary>
    /// Picks an entry out of the Related inventory card's own menu. That card is the one ItemCard on
    /// this page - the checklist's sections are not cards - so it is found by that.
    /// </summary>
    private static void ChooseInStockCheckMenu(IRenderedComponent<TaskListChecklist> cut, string label)
    {
        cut.FindAll(".item-card .overflow-menu-trigger").First().Click();
        cut.FindAll(".item-card .overflow-menu-dropdown .avatar-dropdown-item")
            .First(entry => entry.TextContent.Contains(label))
            .Click();
    }

    /// <summary>Renovation -> Kitchen -> Tiling, plus a plain Garden - two levels below the root.</summary>
    private static IReadOnlyList<TaskDto> ARenovationTree()
    {
        var tiling = TaskList("Tiling", Item("Grout"));
        var kitchen = TaskList("Kitchen", Item("Tiling done", linkedTaskListId: tiling.Id)) with { IsGroup = true };
        var garden = TaskList("Garden", Item("Mow"));
        var renovation = TaskList("Renovation",
            Item("Kitchen done", linkedTaskListId: kitchen.Id),
            Item("Garden done", linkedTaskListId: garden.Id)) with { IsGroup = true };
        return [renovation, kitchen, tiling, garden];
    }
    /// <summary>
    /// Which view a list opens in lives in localStorage, reached through a JS module (see
    /// ChecklistViewPreference) - stubbed as "never saved", so these tests see the tree view unless they
    /// press the button themselves.
    /// </summary>
    private void RegisterChecklistViewPreference(ChecklistViewPreference.SavedReading? saved = null)
    {
        var module = JSInterop.SetupModule("./js/checklistView.js");
        module.Setup<ChecklistViewPreference.SavedReading?>("getSavedReading", _ => true).SetResult(saved);
        module.SetupVoid("saveReading", _ => true);
        Services.AddScoped<ChecklistViewPreference>();
    }
    /// <summary>
    /// The checklist offers to price a group list against a warehouse. These tests are about the items,
    /// so there are no warehouses to choose and the panel stays empty.
    /// </summary>
    private void RegisterInventoryApiClient()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(Array.Empty<WarehouseDto>())
        });
        Services.AddSingleton(new InventoryApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));
    }
}
