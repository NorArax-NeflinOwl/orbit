using System.Net;
using AngleSharp.Dom;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Notifications;
using Orbit.Contracts.Calendar;
using Orbit.Core.Tasks;
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
        // MainLayout owns the polling in the real app; the page only reads this to badge the card a
        // reminder is about, so an empty feed is the right default here.
        Services.AddSingleton(_notifications);
    }

    private readonly NotificationFeedState _notifications = new();

    /// <summary>Opens the menu the sort orders live behind, and picks one by the words on it.</summary>
    /// <summary>
    /// A card's actions live in its overflow menu now - see ItemCard's Menu slot - and OverflowMenu
    /// renders its children only once opened, so they do not exist until this is called. The page's
    /// own sort menu uses the same trigger class, and the card's is the last of them.
    /// </summary>
    private static void OpenTheCardMenu(IRenderedFragment cut)
        => cut.FindAll(".item-card .overflow-menu-trigger").First().Click();

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

        Assert.Equal(2, cut.FindAll(".item-card").Count);
        Assert.Contains("Kitchen", cut.Markup);
        Assert.Contains("Bathroom", cut.Markup);
    }

    /// <summary>
    /// A stray closing brace had ended up in the card's body markup, where Razor rendered it as the
    /// literal text it is - so every expanded card finished with a "}" nobody put there.
    /// </summary>
    [Fact]
    public void An_expanded_card_ends_with_its_list_and_nothing_after_it()
    {
        RegisterTasksApiClient([TaskList("Kitchen", Item("Paint walls"))]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        Assert.DoesNotContain("}", cut.Find(".item-card-body").TextContent);
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

        // The card is for recognising a list; the checklist view is where you work through it. Five is
        // what the normal view shows, which is what the page opens on - see TaskListView.
        Assert.Equal(5, cut.FindAll(".task-preview-row").Count);
        Assert.Contains("and 4 more", cut.Markup);
        Assert.DoesNotContain("Item 9", cut.Markup);
    }

    [Fact]
    public void The_full_view_carries_twenty_of_an_ordinary_lists_items()
    {
        var items = Enumerable.Range(1, 30).Select(number => Item($"Item {number}")).ToArray();
        RegisterTasksApiClient([TaskList("Long one", items)]);
        var cut = RenderComponent<Web.Pages.Tasks>();

        ChooseTheView(cut, "Full");

        // As much as a card can carry before it stops being a card.
        Assert.Equal(20, cut.FindAll(".task-preview-row").Count);
        Assert.Contains("and 10 more", cut.Markup);
    }

    [Fact]
    public void The_full_view_counts_a_group_list_in_member_lists_rather_than_rows()
    {
        // Each member costs five lines - its name, three of its items, and either "and N more…" or the
        // fourth item - so four members is already the twenty lines an ordinary list gets.
        var members = Enumerable.Range(1, 6)
            .Select(number => TaskList(
                $"Member {number}", [.. Enumerable.Range(1, 6).Select(item => Item($"Buy {number}.{item}"))]))
            .ToArray();
        var group = TaskList("Saturday", [.. members.Select(LinkTo)]) with { IsGroup = true };
        RegisterTasksApiClient([group, .. members]);
        var cut = RenderComponent<Web.Pages.Tasks>();

        ChooseTheView(cut, "Full");

        // Four member lists, three items under each. The link rows are the ones naming a member, and the
        // nested rows only exist on the group's card - the members' own cards link to nothing.
        var namedMembers = cut.FindAll(".task-preview-row .row-title")
            .Count(row => row.TextContent.StartsWith("Member ", StringComparison.Ordinal));
        Assert.Equal(4, namedMembers);
        Assert.Equal(12, cut.FindAll(".task-preview-row-linked .row-title").Count);
    }

    [Fact]
    public void A_member_list_of_exactly_four_shows_its_fourth_item_rather_than_a_line_saying_one_is_missing()
    {
        // "and 1 more…" takes exactly the room the row it stands for would have taken.
        var member = TaskList("Shopping", [.. Enumerable.Range(1, 4).Select(number => Item($"Buy {number}"))]);
        var group = TaskList("Saturday", LinkTo(member)) with { IsGroup = true };
        RegisterTasksApiClient([group, member]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        Assert.Equal(4, cut.FindAll(".task-preview-row-linked .row-title").Count);
        Assert.DoesNotContain("and 1 more", cut.Markup);
    }

    [Fact]
    public void The_minimal_view_folds_every_card()
    {
        RegisterTasksApiClient([
            TaskList("Kitchen", Item("Paint walls")),
            TaskList("Garden", Item("Mow the lawn"))
        ]);
        var cut = RenderComponent<Web.Pages.Tasks>();

        ChooseTheView(cut, "Minimal");

        // Every card's own control now says "Expand", which is the tick the brief asked for: the minimal
        // view is the same state as folding each card by hand, so each card shows it as folded.
        var toggles = cut.FindAll("button").Where(button => button.GetAttribute("aria-label") is "Expand" or "Minimise").ToList();
        Assert.Equal(2, toggles.Count);
        Assert.All(toggles, toggle => Assert.Equal("Expand", toggle.GetAttribute("aria-label")));

        // A folded card keeps one line - what is still to be done - so two cards leave two rows.
        Assert.Equal(2, cut.FindAll(".task-preview-row").Count);
    }

    [Fact]
    public void Expanding_a_card_leaves_the_minimal_view_rather_than_unfolding_one_card()
    {
        var items = Enumerable.Range(1, 9).Select(number => Item($"Item {number}")).ToArray();
        RegisterTasksApiClient([TaskList("Long one", items)]);
        var cut = RenderComponent<Web.Pages.Tasks>();
        ChooseTheView(cut, "Full");
        ChooseTheView(cut, "Minimal");

        cut.FindAll("button").First(button => button.GetAttribute("aria-label") == "Expand").Click();

        // Back to what the page was before it was folded away, not to the default - see
        // TaskListArrangement.LeaveMinimalViewAsync.
        Assert.Equal(9, cut.FindAll(".task-preview-row").Count);
    }

    /// <summary>
    /// Opens the menu if it is shut and picks a view. It stays open between choices (see
    /// OverflowMenu.StaysOpen), so pressing the trigger again would close it rather than open it.
    /// </summary>
    private static void ChooseTheView(IRenderedFragment cut, string view)
    {
        if (cut.FindAll(".avatar-dropdown-item").Count == 0)
        {
            cut.Find(".overflow-menu-trigger").Click();
        }

        cut.FindAll(".avatar-dropdown-item").First(option => option.TextContent.Contains(view)).Click();
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

        // The name opens the checklist; the menu is where the rest of a card's actions live. Two
        // ways in, and neither of them a button repeating what the heading already does.
        Assert.Single(cut.FindAll(".item-card-name"));

        OpenTheCardMenu(cut);
        Assert.Contains("Edit", cut.Find(".item-card-menu").TextContent);
    }

    /// <summary>Somebody else's shared list is not this reader's to delete - the same rule the note and
    /// inventory cards already follow.</summary>
    [Fact]
    public void A_shared_list_is_not_offered_for_deleting()
    {
        var shared = TaskList("From Bob", Item("Something")) with { IsShared = true, SharedByUserName = "bob" };
        RegisterTasksApiClient([shared]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        OpenTheCardMenu(cut);
        Assert.DoesNotContain("Delete", cut.Find(".item-card-menu").TextContent);
    }

    [Fact]
    public void Opening_a_list_means_its_checklist_and_the_editor_has_to_be_asked_for()
    {
        var taskList = TaskList("Kitchen", Item("Paint walls"));
        RegisterTasksApiClient([taskList]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<Web.Pages.Tasks>();

        cut.Find(".item-card-name").Click();

        // "/tasks/{id}" is the shallow level, wherever somebody arrives from; the deep editor lives one
        // named click further on, so nothing lands there by default.
        Assert.EndsWith($"/tasks/{taskList.Id}", navigationManager.Uri);

        OpenTheCardMenu(cut);
        cut.FindAll(".item-card-menu button").First(button => button.TextContent.Trim() == "Edit").Click();

        Assert.EndsWith($"/tasks/{taskList.Id}/edit", navigationManager.Uri);
    }

    /// <summary>
    /// The rows of what is on a list are the part of the card a reader points at, and pressing one
    /// opens that entry - the same thing the checklist's own rows and the dashboard's do. They were the
    /// one dead area on the card, and then briefly opened the whole list because the press fell through
    /// to the card behind them.
    /// </summary>
    [Fact]
    public void Pressing_an_entry_on_a_card_opens_that_entry()
    {
        var item = Item("Paint walls");
        var taskList = TaskList("Kitchen", item);
        RegisterTasksApiClient([taskList]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<Web.Pages.Tasks>();

        cut.Find(".item-card-body .task-preview-row").Click();

        Assert.EndsWith($"/tasks/{taskList.Id}/items/{item.Id}", navigationManager.Uri);
    }

    /// <summary>
    /// And the card around them still opens the list, which is what the press stopping at the row is
    /// for: one press, one answer, depending on what was under the pointer.
    /// </summary>
    [Fact]
    public void Pressing_the_card_itself_still_opens_the_list()
    {
        var taskList = TaskList("Kitchen", Item("Paint walls"));
        RegisterTasksApiClient([taskList]);
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<Web.Pages.Tasks>();

        cut.Find(".item-card-body").Click();

        Assert.EndsWith($"/tasks/{taskList.Id}", navigationManager.Uri);
    }

    /// <summary>
    /// An entry that is an appointment carries its event's colour, the same dot the dashboard's Upcoming
    /// card draws. The same appointment used to look like an appointment on one page and like anything
    /// else on the other.
    /// </summary>
    [Fact]
    public void An_appointment_on_a_card_carries_its_events_colour()
    {
        var eventId = Guid.NewGuid();
        var appointment = Item("Dentist") with
        {
            Kind = nameof(TaskItemKind.Calendar), LinkedCalendarEventId = eventId
        };
        RegisterTasksApiClient([TaskList("Kitchen", appointment)], [AnEventColoured(eventId, "#ff0000")]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        var dot = cut.Find(".item-card-body .task-preview-row .stat-dot");
        Assert.Contains("#ff0000", dot.GetAttribute("style"));
    }

    [Fact]
    public void An_ordinary_entry_carries_no_colour_at_all()
    {
        RegisterTasksApiClient([TaskList("Kitchen", Item("Paint walls"))]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        // A thing to tick off is not an appointment, so there is no event and nothing to colour it with.
        Assert.Empty(cut.FindAll(".item-card-body .stat-dot"));
    }

    /// <summary>
    /// The chips are built from what the entries are actually filed under, and pressing one narrows the
    /// page to the lists still holding something - see TaskItemFilter.
    /// </summary>
    [Fact]
    public void Choosing_a_category_leaves_only_the_lists_that_have_one()
    {
        RegisterTasksApiClient([
            TaskList("Kitchen", Item("Buy milk") with { Categories = ["shopping"] }),
            TaskList("Garage", Item("New tyres") with { Categories = ["car"] })]);
        var cut = RenderComponent<Web.Pages.Tasks>();

        cut.FindAll(".filter-chip").First(chip => chip.TextContent.Contains("shopping")).Click();

        var shown = cut.FindAll(".item-card-name").Select(card => card.TextContent.Trim()).ToList();
        Assert.Equal(["Kitchen"], shown);
    }

    /// <summary>
    /// The chips filter by what entries are filed under, so the rows have to say it - otherwise the
    /// reader takes the page's word for why a list is still here.
    /// </summary>
    [Fact]
    public void A_row_says_what_it_is_filed_under_and_marks_the_one_being_filtered_by()
    {
        RegisterTasksApiClient([TaskList("Kitchen", Item("Buy milk") with { Categories = ["shopping", "weekly"] })]);
        var cut = RenderComponent<Web.Pages.Tasks>();

        Assert.Equal(
            ["shopping", "weekly"],
            cut.FindAll(".task-preview-row .row-category").Select(category => category.TextContent.Trim()));

        cut.FindAll(".filter-chip").First(chip => chip.TextContent.Contains("weekly")).Click();

        var marked = cut.FindAll(".task-preview-row .row-category.on").Select(category => category.TextContent.Trim());
        Assert.Equal(["weekly"], marked);
    }

    /// <summary>
    /// A word from an entry, not from the list's own name: the box is there to find the thing that has
    /// to be done, which is what a reader remembers and what the card only shows a few of.
    /// </summary>
    [Fact]
    public void Searching_finds_the_list_an_entry_is_on()
    {
        RegisterTasksApiClient([TaskList("Kitchen", Item("Buy milk")), TaskList("Garage", Item("New tyres"))]);
        var cut = RenderComponent<Web.Pages.Tasks>();

        cut.Find(".task-search input").Input("tyre");

        var shown = cut.FindAll(".item-card-name").Select(card => card.TextContent.Trim()).ToList();
        Assert.Equal(["Garage"], shown);
    }

    /// <summary>With something being looked for, a card shows what matched rather than its first few rows.</summary>
    [Fact]
    public void A_narrowed_card_shows_what_matched()
    {
        RegisterTasksApiClient([TaskList("Kitchen", Item("Buy milk"), Item("Buy bread"))]);
        var cut = RenderComponent<Web.Pages.Tasks>();

        cut.Find(".task-search input").Input("milk");

        var rows = cut.FindAll(".item-card-body .task-preview-row .row-title")
            .Select(row => row.TextContent.Trim())
            .ToList();
        Assert.Equal(["Buy milk"], rows);
    }

    [Fact]
    public void An_account_with_no_lists_gets_a_hint_instead_of_an_empty_grid()
    {
        RegisterTasksApiClient([]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        Assert.Contains("No task lists yet", cut.Markup);
        Assert.Empty(cut.FindAll(".item-card"));
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
        // All, the four statuses, and the two chips that are about what a list is rather than how far
        // along it is: where it came from, and whether it gathers other lists.
        Assert.Equal(7, cut.FindAll(".filter-chip").Count);
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

        Assert.Contains("From Bob", cut.Find(".task-card-grid").InnerHtml);
        Assert.DoesNotContain("Kitchen", cut.Find(".task-card-grid").InnerHtml);
    }

    [Fact]
    public void The_group_filter_shows_only_the_lists_that_gather_others()
    {
        var member = TaskList("Shopping", Item("Milk"));
        var group = TaskList("Saturday", LinkTo(member)) with { IsGroup = true };
        RegisterTasksApiClient([group, member]);
        var cut = RenderComponent<Web.Pages.Tasks>();

        cut.FindAll(".filter-chip").First(chip => chip.TextContent.Contains("Group")).Click();

        // One card, and it is the group. Asserted by counting rather than by looking for the member's
        // title, which legitimately appears inside the group's card as the row that points at it.
        var card = Assert.Single(cut.FindAll(".item-card"));
        Assert.Contains("Saturday", card.QuerySelector(".item-card-name")!.TextContent);
    }

    [Fact]
    public void A_minimised_card_keeps_its_heading_one_row_and_its_buttons()
    {
        RegisterTasksApiClient([TaskList("Kitchen", Item("Paint walls", isCompleted: true), Item("Fit worktop"), Item("Tile"))]);
        var cut = RenderComponent<Web.Pages.Tasks>();

        cut.FindAll(".item-card .icon-btn").First(button => button.GetAttribute("title") == "Minimise").Click();

        // One row, and the one worth having: what is still to be done. The heading and the buttons stay.
        var row = Assert.Single(cut.FindAll(".task-preview-row"));
        Assert.Contains("Fit worktop", row.TextContent);
        Assert.DoesNotContain("Tile", cut.Find(".task-card-grid").InnerHtml);
        Assert.Contains("Kitchen", cut.Find(".item-card-name").TextContent);
        OpenTheCardMenu(cut);
        Assert.Contains("Edit", cut.Find(".item-card-menu").TextContent);
    }

    [Fact]
    public void A_minimised_card_can_be_brought_back()
    {
        RegisterTasksApiClient([TaskList("Kitchen", Item("Paint walls"), Item("Fit worktop"))]);
        var cut = RenderComponent<Web.Pages.Tasks>();
        cut.FindAll(".item-card .icon-btn").First(button => button.GetAttribute("title") == "Minimise").Click();

        cut.FindAll(".item-card .icon-btn").First(button => button.GetAttribute("title") == "Expand").Click();

        Assert.Equal(2, cut.FindAll(".task-preview-row").Count);
    }

    [Fact]
    public void A_list_something_unread_is_about_says_so_in_front_of_its_name()
    {
        var quiet = TaskList("Kitchen", Item("Paint walls"));
        var reminded = TaskList("Garden", Item("Mow"));
        RegisterTasksApiClient([quiet, reminded]);
        _notifications.Set([Notification($"/tasks/{reminded.Id}")]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        // A page of cards should answer "which list was that reminder about?" without opening any.
        var flagged = Assert.Single(cut.FindAll(".item-card"), card => card.QuerySelector(".item-card-action") is not null);
        Assert.Contains("Garden", flagged.QuerySelector(".item-card-name")!.TextContent);
        // And says it on its own edge as well as with the dot: nine pixels is easy to miss on a page of
        // cards, and this is the one worth stopping at.
        Assert.Contains("item-card-unseen", flagged.ClassName);
        Assert.DoesNotContain(
            "item-card-unseen",
            Assert.Single(cut.FindAll(".item-card"), card => card.QuerySelector(".item-card-action") is null).ClassName);
    }

    private static NotificationEntryDto Notification(string url)
        => new(Guid.NewGuid(), "TaskOverdue", "Overdue task", "Body", url, DateTimeOffset.UtcNow, IsRead: false);

    [Fact]
    public void A_row_pointing_at_another_list_shows_what_is_on_it()
    {
        var member = TaskList("Shopping", Item("Milk"), Item("Bread"));
        var group = TaskList("Saturday", LinkTo(member)) with { IsGroup = true };
        RegisterTasksApiClient([group, member]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        // Otherwise a group list's card is a stack of titles, which says nothing about the work.
        var groupCard = cut.FindAll(".item-card").First(card => card.TextContent.Contains("Saturday"));
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
        Assert.Empty(cut.FindAll(".item-card .drag-handle"));

        SortBy(cut, "The way I arranged them");

        Assert.Equal(2, cut.FindAll(".item-card .drag-handle").Count);
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
        Assert.DoesNotContain("Garden", cut.Find(".task-card-grid").InnerHtml);
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

        // Left of the name, which is where every card in the app now carries it - see ItemCard. It
        // used to sit in the top right beside the count; the anatomy moved it, deliberately, so that
        // the things describing what a card *is* all read from the same edge.
        Assert.Single(cut.FindAll(".item-card-head .item-card-pin .pin-button"));
        Assert.Empty(cut.FindAll(".item-card-menu .pin-button"));
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

        Assert.DoesNotContain("Pin", cut.Find(".item-card-menu").TextContent);
    }

    /// <summary>The card titles in the order they render, each still carrying its badges' text after the title itself.</summary>
    private static string[] CardTitles(IRenderedFragment cut)
        => cut.FindAll(".item-card-name").Select(title => title.TextContent.Trim()).ToArray();


    [Fact]
    public void A_minimised_group_card_says_what_is_still_to_be_done_on_its_members()
    {
        // A group's rows only point at other lists, so looking for work among them found none and the
        // folded card said "Nothing left to do." over six open errands.
        var member = TaskList("Recipes", Item("Buy flour"));
        var group = TaskList("Cooking", LinkTo(member));
        RegisterTasksApiClient([group, member]);
        var cut = RenderComponent<Web.Pages.Tasks>();

        MinimiseTheCardFor(cut, "Cooking");

        var row = FoldedRowOf(cut, "Cooking");
        Assert.Contains("Buy flour", row.TextContent);
        // And where it came from, since the errand is not on the card's own list.
        Assert.Contains("Recipes", row.TextContent);
        Assert.DoesNotContain("Nothing left to do", row.TextContent);
    }

    [Fact]
    public void A_minimised_group_card_with_nothing_left_anywhere_still_says_so()
    {
        // The guard on the fix above: it must find work through a link, not invent it.
        var member = TaskList("Recipes", Item("Buy flour", isCompleted: true));
        var group = TaskList("Cooking", LinkTo(member) with { IsCompleted = true });
        RegisterTasksApiClient([group, member]);
        var cut = RenderComponent<Web.Pages.Tasks>();

        MinimiseTheCardFor(cut, "Cooking");

        Assert.Contains("Nothing left to do", FoldedRowOf(cut, "Cooking").TextContent);
    }

    [Fact]
    public void A_list_one_item_over_the_preview_is_shown_in_full()
    {
        // "and 1 more…" takes exactly the room the row it stands for would have taken, so hiding that
        // row saves nothing and costs the reader the one thing it was hiding.
        var items = Enumerable.Range(1, 5).Select(number => Item($"Item {number}")).ToArray();
        RegisterTasksApiClient([TaskList("Shopping", items)]);

        var cut = RenderComponent<Web.Pages.Tasks>();

        Assert.Equal(5, cut.FindAll(".task-preview-row").Count);
        Assert.Contains("Item 5", cut.Markup);
        Assert.DoesNotContain("and 1 more", cut.Markup);
    }

    /// <summary>Folds the card whose title says this, whichever order the cards happen to be in.</summary>
    private static void MinimiseTheCardFor(IRenderedFragment cut, string title)
        => CardFor(cut, title).QuerySelectorAll(".icon-btn")
            .First(button => button.GetAttribute("title") == "Minimise")
            .Click();

    private static IElement FoldedRowOf(IRenderedFragment cut, string title)
        => CardFor(cut, title).QuerySelector(".list-row")!;

    private static IElement CardFor(IRenderedFragment cut, string title)
        => cut.FindAll(".item-card")
            .First(card => card.QuerySelector(".item-card-name")!.TextContent.Contains(title, StringComparison.Ordinal));
    private static CalendarEventDto AnEventColoured(Guid eventId, string colour) => new(
        eventId,
        new CalendarEventDetailsDto(
            "Dentist", Description: null, Location: null, Color: colour,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), IsAllDay: false, Recurrence: null,
            Guests: [], ReminderMinutesBeforeStart: [], ReminderNotificationChannel: "None", Priority: "Normal"),
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
        IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);

    /// <param name="events">
    /// The calendar, which this page reads only for the colour of an entry that is an appointment - see
    /// Tasks.EventColourOf. Empty unless a test is about that, and on its own stub because the task
    /// handler answers every request with task lists.
    /// </param>
    private void RegisterTasksApiClient(
        IReadOnlyList<TaskDto> taskLists, IReadOnlyList<CalendarEventDto>? events = null)
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(taskLists) });
        Services.AddSingleton(new TasksApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));

        var calendarHandler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(events ?? []) });
        Services.AddSingleton(new CalendarApiClient(
            new HttpClient(calendarHandler) { BaseAddress = new Uri("https://example.test/") }));
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
