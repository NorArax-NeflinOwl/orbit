using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Tasks;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Orbit.Web.Tests;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// Covers the task list overview: one card per list showing enough to recognise it, rather than every
/// item of every list at once.
/// </summary>
public sealed class TasksTests : OrbitTestContext
{
    public TasksTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        Services.AddSingleton(new TaskListArrangement(new StubJSRuntime()));
    }

    /// <summary>Opens the menu the sort orders live behind, and picks one by the words on it.</summary>
    private static void SortBy(IRenderedFragment cut, string label)
    {
        if (cut.FindAll(".overflow-menu-dropdown").Count == 0)
        {
            cut.Find(".overflow-menu-trigger").Click();
        }

        cut.FindAll(".overflow-menu-dropdown .avatar-dropdown-item")
            .First(option => option.TextContent.Contains(label))
            .Click();
    }

    [Fact]
    public void Each_task_list_gets_its_own_card()
    {
        RegisterTasksApiClient([TaskList("Kitchen", Item("Paint walls")), TaskList("Bathroom", Item("Replace tap"))]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        Assert.Equal(2, cut.FindAll(".task-list-card").Count);
        Assert.Contains("Kitchen", cut.Markup);
        Assert.Contains("Bathroom", cut.Markup);
    }

    [Fact]
    public void A_card_shows_how_far_through_its_list_you_are()
    {
        RegisterTasksApiClient([TaskList("Kitchen", Item("Paint walls", isCompleted: true), Item("Fit worktop"))]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        Assert.Contains("1/2", cut.Find(".card-count").TextContent);
    }

    [Fact]
    public void An_empty_list_says_so_rather_than_showing_a_count()
    {
        RegisterTasksApiClient([TaskList("Someday")]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        Assert.Contains("empty", cut.Find(".card-count").TextContent);
        Assert.Contains("No items on this list", cut.Markup);
    }

    [Fact]
    public void A_long_list_is_previewed_rather_than_printed_in_full()
    {
        var items = Enumerable.Range(1, 9).Select(number => Item($"Item {number}")).ToArray();
        RegisterTasksApiClient([TaskList("Long one", items)]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        // The card is for recognising a list; the checklist view is where you work through it.
        Assert.Equal(4, cut.FindAll(".task-preview-row").Count);
        Assert.Contains("and 5 more", cut.Markup);
        Assert.DoesNotContain("Item 9", cut.Markup);
    }

    [Fact]
    public void A_completed_item_is_struck_through_in_the_preview()
    {
        RegisterTasksApiClient([TaskList("Kitchen", Item("Paint walls", isCompleted: true))]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        Assert.Contains("completed", cut.Find(".task-preview-row").ClassName);
    }

    [Fact]
    public void Group_and_shared_lists_are_labelled()
    {
        var group = TaskList("Renovation", Item("Kitchen done")) with { IsGroup = true };
        var shared = TaskList("From Bob", Item("Something")) with { IsShared = true, SharedByUserName = "bob" };
        RegisterTasksApiClient([group, shared]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        var badges = cut.FindAll(".card-badge").Select(badge => badge.TextContent).ToArray();
        Assert.Contains("Group", badges);
        Assert.Contains("Shared", badges);
    }

    [Fact]
    public void Both_ways_into_a_list_are_offered()
    {
        RegisterTasksApiClient([TaskList("Kitchen", Item("Paint walls"))]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        var actions = cut.Find(".card-actions").TextContent;
        Assert.Contains("Open checklist", actions);
        Assert.Contains("Edit", actions);
    }

    [Fact]
    public void Opening_a_list_means_its_checklist_and_the_editor_has_to_be_asked_for()
    {
        var taskList = TaskList("Kitchen", Item("Paint walls"));
        RegisterTasksApiClient([taskList]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<Web.Pages.Tasks>();

        cut.FindAll(".card-actions button").First(button => button.TextContent.Contains("Open checklist")).Click();

        // "/tasks/{id}" is the shallow level, wherever somebody arrives from; the deep editor lives one
        // named click further on, so nothing lands there by default.
        Assert.EndsWith($"/tasks/{taskList.Id}", navigationManager.Uri);

        cut.FindAll(".card-actions button").First(button => button.TextContent.Trim() == "Edit").Click();

        Assert.EndsWith($"/tasks/{taskList.Id}/edit", navigationManager.Uri);
    }

    [Fact]
    public void An_account_with_no_lists_gets_a_hint_instead_of_an_empty_grid()
    {
        RegisterTasksApiClient([]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        Assert.Contains("No task lists yet", cut.Markup);
        Assert.Empty(cut.FindAll(".task-list-card"));
    }

    [Fact]
    public void Every_status_gets_a_filter_showing_how_many_are_in_it()
    {
        RegisterTasksApiClient([
            TaskList("Kitchen", "Normal", "Overdue", DateTimeOffset.UtcNow),
            TaskList("Bathroom", "Normal", "Overdue", DateTimeOffset.UtcNow),
            TaskList("Garden", "Normal", "Done", DateTimeOffset.UtcNow)]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        Assert.Contains("Overdue", cut.Markup);
        // All, the four statuses, and "Shared" - which is about where a list came from rather than
        // how far along it is, but is asked in the same breath.
        Assert.Equal(6, cut.FindAll(".filter-chip").Count);
    }

    [Fact]
    public void The_shared_filter_shows_only_what_somebody_else_owns()
    {
        var mine = TaskList("Kitchen", "Normal", "New", DateTimeOffset.UtcNow);
        var theirs = TaskList("From Bob", "Normal", "New", DateTimeOffset.UtcNow) with
        {
            IsShared = true, SharedByUserName = "bob", AccessLevel = "ReadOnly"
        };
        RegisterTasksApiClient([mine, theirs]);
        var cut = RenderComponent<Web.Pages.Tasks>();

        cut.FindAll(".filter-chip").First(chip => chip.TextContent.Contains("Shared")).Click();

        Assert.Contains("From Bob", cut.Find(".card-grid").InnerHtml);
        Assert.DoesNotContain("Kitchen", cut.Find(".card-grid").InnerHtml);
    }

    [Fact]
    public void A_row_pointing_at_another_list_shows_what_is_on_it()
    {
        var member = TaskList("Shopping", Item("Milk"), Item("Bread"));
        var group = TaskList("Saturday", LinkTo(member)) with { IsGroup = true };
        RegisterTasksApiClient([group, member]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        // Otherwise a group list's card is a stack of titles, which says nothing about the work.
        var groupCard = cut.FindAll(".task-list-card").First(card => card.TextContent.Contains("Saturday"));
        Assert.Contains("Milk", groupCard.TextContent);
        Assert.Contains("Bread", groupCard.TextContent);
    }

    [Fact]
    public void Only_the_order_somebody_arranged_themselves_can_be_dragged()
    {
        RegisterTasksApiClient([
            TaskList("Kitchen", "Normal", "New", DateTimeOffset.UtcNow),
            TaskList("Garden", "Normal", "New", DateTimeOffset.UtcNow)]);
        var cut = RenderComponent<Web.Pages.Tasks>();

        // Moving a card by hand under any other order would not survive the next redraw.
        Assert.Empty(cut.FindAll(".task-list-card .drag-handle"));

        SortBy(cut, "The way I arranged them");

        Assert.Equal(2, cut.FindAll(".task-list-card .drag-handle").Count);
    }

    [Fact]
    public void Filtering_by_a_status_hides_the_rest()
    {
        RegisterTasksApiClient([
            TaskList("Kitchen", "Normal", "Overdue", DateTimeOffset.UtcNow),
            TaskList("Garden", "Normal", "Completed", DateTimeOffset.UtcNow)]);
        var cut = RenderComponent<Web.Pages.Tasks>();

        cut.FindAll(".filter-chip").First(chip => chip.TextContent.Contains("Overdue")).Click();

        Assert.Contains("Kitchen", cut.Markup);
        Assert.DoesNotContain("Garden", cut.Find(".card-grid").InnerHtml);
    }

    [Fact]
    public void Sorting_by_priority_puts_the_high_ones_first()
    {
        RegisterTasksApiClient([
            TaskList("Low one", "Low", "New", DateTimeOffset.UtcNow),
            TaskList("High one", "High", "New", DateTimeOffset.UtcNow),
            TaskList("Normal one", "Normal", "New", DateTimeOffset.UtcNow)]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        // Priority is the default order: it is the one the reader set themselves.
        // StartsWith rather than Equal: the title cell also carries the status and priority badges.
        var titles = CardTitles(cut);
        Assert.StartsWith("High one", titles[0]);
        Assert.StartsWith("Low one", titles[^1]);
    }

    [Fact]
    public void Sorting_alphabetically_orders_by_title()
    {
        RegisterTasksApiClient([
            TaskList("Zebra", "Normal", "New", DateTimeOffset.UtcNow),
            TaskList("Apple", "Normal", "New", DateTimeOffset.UtcNow)]);
        var cut = RenderComponent<Web.Pages.Tasks>();

        SortBy(cut, "A to Z");

        Assert.StartsWith("Apple", CardTitles(cut)[0]);
    }

    [Fact]
    public void Sorting_by_oldest_starts_with_the_oldest()
    {
        RegisterTasksApiClient([
            TaskList("Recent", "Normal", "New", DateTimeOffset.UtcNow),
            TaskList("Ancient", "Normal", "New", DateTimeOffset.UtcNow.AddYears(-1))]);
        var cut = RenderComponent<Web.Pages.Tasks>();

        SortBy(cut, "Oldest first");

        Assert.StartsWith("Ancient", CardTitles(cut)[0]);
    }

    [Fact]
    public void A_filter_that_matches_nothing_says_so_rather_than_showing_an_empty_grid()
    {
        RegisterTasksApiClient([TaskList("Kitchen", "Normal", "New", DateTimeOffset.UtcNow)]);
        var cut = RenderComponent<Web.Pages.Tasks>();

        cut.FindAll(".filter-chip").First(chip => chip.TextContent.Contains("Done")).Click();

        Assert.Contains("No lists are", cut.Markup);
    }

    [Fact]
    public void A_pinned_list_leads_the_page()
    {
        RegisterTasksApiClient([
            TaskList("Zebra", "High", "New", DateTimeOffset.UtcNow),
            TaskList("Apple", "Low", "New", DateTimeOffset.UtcNow, isPinned: true)]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        // Low priority, and still first: that is what pinning is for.
        Assert.StartsWith("Apple", CardTitles(cut)[0]);
    }

    [Fact]
    public void A_pin_holds_under_every_sort_order()
    {
        RegisterTasksApiClient([
            TaskList("Apple", "High", "New", DateTimeOffset.UtcNow.AddYears(-1)),
            TaskList("Zebra", "Low", "New", DateTimeOffset.UtcNow, isPinned: true)]);
        var cut = RenderComponent<Web.Pages.Tasks>();

        foreach (var order in new[] { "A to Z", "Z to A", "Newest first", "Oldest first", "Most important first" })
        {
            SortBy(cut, order);
            Assert.StartsWith("Zebra", CardTitles(cut)[0]);
        }
    }

    [Fact]
    public void A_pinned_list_says_so()
    {
        RegisterTasksApiClient([TaskList("Kitchen", "Normal", "New", DateTimeOffset.UtcNow, isPinned: true)]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        Assert.Single(cut.FindAll(".card-badge-pinned"));
        Assert.Contains("Unpin", cut.Markup);
    }

    [Fact]
    public void The_pin_sits_where_the_rest_of_the_app_puts_it()
    {
        RegisterTasksApiClient([TaskList("Kitchen", "Normal", "New", DateTimeOffset.UtcNow)]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        // Top right of the card, beside the count - the same corner a note and a dashboard card use.
        Assert.Single(cut.FindAll(".card-header .card-header-end .pin-button"));
        Assert.Empty(cut.FindAll(".card-actions .pin-button"));
    }

    [Fact]
    public void A_list_you_only_hold_a_share_of_offers_no_pin()
    {
        // Pinning moves the card on its owner's page, so a recipient pinning it would be rearranging
        // someone else's.
        var shared = TaskList("Theirs", "Normal", "New", DateTimeOffset.UtcNow) with
        {
            IsShared = true, SharedByUserName = "anna", AccessLevel = "ReadOnly"
        };
        RegisterTasksApiClient([shared]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        Assert.DoesNotContain("Pin", cut.Find(".card-actions").TextContent);
    }

    /// <summary>The card titles in the order they render, each still carrying its badges' text after the title itself.</summary>
    private static string[] CardTitles(IRenderedFragment cut)
        => cut.FindAll(".card-title").Select(title => title.TextContent.Trim()).ToArray();

    private void RegisterTasksApiClient(IReadOnlyList<TaskDto> taskLists)
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(taskLists) });
        Services.AddSingleton(new TasksApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));
    }

    private static TaskDto TaskList(string title, params TaskItemDto[] items)
        => TaskList(title, "Normal", "New", DateTimeOffset.UtcNow, items);

    private static TaskDto TaskList(
        string title, string priority, string status, DateTimeOffset createdAtUtc, params TaskItemDto[] items)
        => TaskList(title, priority, status, createdAtUtc, isPinned: false, items);

    private static TaskDto TaskList(
        string title, string priority, string status, DateTimeOffset createdAtUtc, bool isPinned, params TaskItemDto[] items)
        => new(
            Guid.NewGuid(), title, items, IsCompleted: false, IsGroup: false, IsPrivate: false, EncryptedContent: null,
            createdAtUtc, createdAtUtc,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null,
            Priority: priority, Status: status, IsPinned: isPinned);

    /// <summary>A row that only points at another list - how a group list gathers its members.</summary>
    private static TaskItemDto LinkTo(TaskDto member)
        => Item(member.Title) with { LinkedTaskListId = member.Id };

    private static TaskItemDto Item(string description, bool isCompleted = false)
        => new(
            Guid.NewGuid(), description, DueDateUtc: null, isCompleted, LinkedTaskListId: null,
            OverdueNotificationChannel: "None", RemindDaily: false,
            DailyReminderNotificationChannel: "None", DailyReminderTimeOfDay: default);
}
