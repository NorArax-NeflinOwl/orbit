using Orbit.Contracts.Tasks;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Whether linking one list to another would close a loop. The same rule the server enforces, asked
/// here so the editor never offers a link the save would refuse - the failure it replaces was a save
/// naming a rule nothing on screen had mentioned, and the longer the chain the less obvious what had
/// gone wrong.
/// </summary>
public sealed class TaskListLinkCycleTests
{
    private static readonly Guid ShoppingId = Guid.NewGuid();
    private static readonly Guid RecipesId = Guid.NewGuid();
    private static readonly Guid PartyId = Guid.NewGuid();
    private static readonly Guid UnrelatedId = Guid.NewGuid();

    [Fact]
    public void A_list_cannot_link_to_itself()
    {
        Assert.True(TaskListLinkCycle.WouldClose(Lists(), ShoppingId, ShoppingId));
    }

    [Fact]
    public void A_list_that_links_nowhere_is_a_fine_thing_to_link_to()
    {
        Assert.False(TaskListLinkCycle.WouldClose(Lists(), ShoppingId, UnrelatedId));
    }

    [Fact]
    public void A_list_that_links_straight_back_would_close_a_loop()
    {
        // Recipes links to Shopping, so Shopping linking to Recipes closes it in one step.
        Assert.True(TaskListLinkCycle.WouldClose(Lists(), ShoppingId, RecipesId));
    }

    [Fact]
    public void A_list_that_links_back_through_a_chain_would_close_one_too()
    {
        // Party links to Recipes, Recipes links to Shopping. This is the case the editor could not see:
        // three lists deep, the row offering it looks like any other.
        Assert.True(TaskListLinkCycle.WouldClose(Lists(), ShoppingId, PartyId));
    }

    [Fact]
    public void A_loop_somewhere_else_does_not_stop_the_walk()
    {
        // Two lists pointing at each other, neither reaching the one being edited. Without marking what
        // has been walked, this is where the search never returns.
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var lists = new List<TaskDto>
        {
            List(ShoppingId, "Shopping"),
            List(first, "First", LinkTo(second)),
            List(second, "Second", LinkTo(first))
        };

        Assert.False(TaskListLinkCycle.WouldClose(lists, ShoppingId, first));
    }

    [Fact]
    public void A_link_to_a_list_this_reader_cannot_see_ends_that_branch()
    {
        // The walk cannot follow what it was not given, and the server refuses such a link for its own
        // reasons - so the answer here is "no loop I can see" rather than a crash.
        var missing = Guid.NewGuid();
        var lists = new List<TaskDto> { List(ShoppingId, "Shopping"), List(UnrelatedId, "Errands", LinkTo(missing)) };

        Assert.False(TaskListLinkCycle.WouldClose(lists, ShoppingId, UnrelatedId));
    }

    /// <summary>Party → Recipes → Shopping, plus one list off on its own.</summary>
    private static IReadOnlyList<TaskDto> Lists()
        =>
        [
            List(ShoppingId, "Shopping"),
            List(RecipesId, "Recipes", LinkTo(ShoppingId)),
            List(PartyId, "Party", LinkTo(RecipesId)),
            List(UnrelatedId, "Errands")
        ];

    private static TaskDto List(Guid id, string title, params TaskItemDto[] items)
        => new(
            id, title, items, IsCompleted: false, IsGroup: false, IsPrivate: false, EncryptedContent: null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);

    private static TaskItemDto LinkTo(Guid taskListId)
        => new(
            Guid.NewGuid(), "Follows another list", DueDateUtc: null, IsCompleted: false, taskListId,
            OverdueNotificationChannel: "None", RemindDaily: false,
            DailyReminderNotificationChannel: "None", DailyReminderTimeOfDay: new TimeOnly(9, 0));
}
