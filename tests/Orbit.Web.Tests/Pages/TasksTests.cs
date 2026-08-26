using System.Net;
using System.Net.Http.Json;
using Bunit;
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
    public TasksTests() => Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

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
        Assert.Equal(5, cut.FindAll(".filter-chip").Count);
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

        cut.Find(".list-sort select").Change("Alphabetical");

        Assert.StartsWith("Apple", CardTitles(cut)[0]);
    }

    [Fact]
    public void Sorting_by_oldest_starts_with_the_oldest()
    {
        RegisterTasksApiClient([
            TaskList("Recent", "Normal", "New", DateTimeOffset.UtcNow),
            TaskList("Ancient", "Normal", "New", DateTimeOffset.UtcNow.AddYears(-1))]);
        var cut = RenderComponent<Web.Pages.Tasks>();

        cut.Find(".list-sort select").Change("Oldest");

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

        foreach (var order in new[] { "Alphabetical", "ReverseAlphabetical", "Newest", "Oldest", "Priority" })
        {
            cut.Find(".list-sort select").Change(order);
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

    private static TaskItemDto Item(string description, bool isCompleted = false)
        => new(
            Guid.NewGuid(), description, DueDateUtc: null, isCompleted, LinkedTaskListId: null,
            OverdueNotificationChannel: "None", RemindDaily: false,
            DailyReminderNotificationChannel: "None", DailyReminderTimeOfDay: default);
}
