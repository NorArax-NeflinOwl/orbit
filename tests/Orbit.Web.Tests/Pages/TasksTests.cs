using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Tasks;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// Covers the task list overview: one card per list showing enough to recognise it, rather than every
/// item of every list at once.
/// </summary>
public sealed class TasksTests : TestContext
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

    private void RegisterTasksApiClient(IReadOnlyList<TaskDto> taskLists)
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(taskLists) });
        Services.AddSingleton(new TasksApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));
    }

    private static TaskDto TaskList(string title, params TaskItemDto[] items)
        => new(
            Guid.NewGuid(), title, items, IsCompleted: false, IsGroup: false,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);

    private static TaskItemDto Item(string description, bool isCompleted = false)
        => new(
            Guid.NewGuid(), description, DueDateUtc: null, isCompleted, LinkedTaskListId: null,
            OverdueNotificationChannel: "None", RemindDaily: false,
            DailyReminderNotificationChannel: "None", DailyReminderTimeOfDay: default);
}
