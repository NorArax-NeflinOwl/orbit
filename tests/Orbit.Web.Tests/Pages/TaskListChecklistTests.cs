using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public void An_item_that_follows_another_list_cannot_be_ticked_by_hand()
    {
        var kitchen = TaskList("Kitchen", Item("Paint walls"));
        var group = TaskList("Renovation", Item("Kitchen done", linkedTaskListId: kitchen.Id)) with { IsGroup = true };
        RegisterTasksApiClient([group, kitchen]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, group.Id));

        // Its completion is derived from the linked list (see LinkedTaskCompletionResolver), so setting
        // it here would be a change the next reload silently undoes.
        var linkedCheckbox = cut.FindAll(".check-row input[type=checkbox]").ToArray()[0];
        Assert.True(linkedCheckbox.HasAttribute("disabled"));
        Assert.Contains("follows Kitchen", cut.Markup);
    }

    [Fact]
    public void A_read_only_share_renders_every_item_as_look_but_do_not_touch()
    {
        var taskList = TaskList("Errands", Item("Buy milk")) with { AccessLevel = "ReadOnly", IsShared = true };
        RegisterTasksApiClient([taskList]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, taskList.Id));

        Assert.True(cut.Find(".check-row input[type=checkbox]").HasAttribute("disabled"));
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


    [Fact]
    public void A_due_item_offers_to_go_into_google_calendar()
    {
        _googleExtrasAvailable = true;
        var taskList = TaskList("Admin", Item("File the return", dueDateUtc: new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero)));
        RegisterTasksApiClient([taskList]);

        var cut = RenderComponent<TaskListChecklist>(parameters => parameters.Add(page => page.Id, taskList.Id));

        var link = cut.Find("a.google-link");
        Assert.Contains("calendar.google.com", link.GetAttribute("href"));
        // Opens away from Orbit, and without handing Google a referrer that names the page.
        Assert.Equal("_blank", link.GetAttribute("target"));
        Assert.Equal("noopener", link.GetAttribute("rel"));
    }

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
            NullLogger<GoogleIntegrationAccess>.Instance));
    }

    private void RegisterTasksApiClient(IReadOnlyList<TaskDto> taskLists)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            _requests.Add(request);
            _requestBodies.Add(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty);
            return request.Method == HttpMethod.Put
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(taskLists) };
        });
        Services.AddSingleton(new TasksApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));
    }

    private static TaskDto TaskList(string title, params TaskItemDto[] items)
        => new(
            Guid.NewGuid(), title, items, IsCompleted: false, IsGroup: false, IsPrivate: false, EncryptedContent: null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);

    private static TaskItemDto Item(
        string description, bool isCompleted = false, Guid? linkedTaskListId = null, DateTimeOffset? dueDateUtc = null)
        => new(
            Guid.NewGuid(), description, dueDateUtc, isCompleted, linkedTaskListId,
            OverdueNotificationChannel: "None", RemindDaily: false,
            DailyReminderNotificationChannel: "None", DailyReminderTimeOfDay: default);
}
