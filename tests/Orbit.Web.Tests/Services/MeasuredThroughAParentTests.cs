using Orbit.Contracts.Tasks;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Which lists a shelf is measured against without anybody having ticked them. Measuring a group list
/// measures everything it gathers - that is what gathering means - so a shelf serving a group of a dozen
/// lists used to show one box ticked and eleven unticked, which reads as "these eleven ask for nothing
/// here". The walk below is what turns those eleven into rows that say which group they came through.
/// </summary>
public sealed class MeasuredThroughAParentTests
{
    private static readonly Guid ShoppingId = Guid.NewGuid();
    private static readonly Guid RecipesId = Guid.NewGuid();
    private static readonly Guid PastaId = Guid.NewGuid();
    private static readonly Guid DoctorId = Guid.NewGuid();

    [Fact]
    public void Nothing_is_reached_when_no_list_is_measured_here()
    {
        Assert.Empty(MeasuredThroughAParent.Reach(Lists(), new HashSet<Guid>()));
    }

    [Fact]
    public void A_list_the_measured_group_gathers_is_reached_through_it()
    {
        var reached = MeasuredThroughAParent.Reach(Lists(), new HashSet<Guid> { ShoppingId });

        Assert.Equal(ShoppingId, reached[RecipesId]);
    }

    [Fact]
    public void A_list_two_levels_down_is_reached_through_the_group_that_was_ticked()
    {
        // Shopping gathers Recipes, Recipes gathers Pasta. The row on Pasta has to name Shopping - the
        // list somebody actually ticked - rather than Recipes, which nobody said anything about.
        var reached = MeasuredThroughAParent.Reach(Lists(), new HashSet<Guid> { ShoppingId });

        Assert.Equal(ShoppingId, reached[PastaId]);
    }

    [Fact]
    public void A_list_nothing_gathers_is_not_reached()
    {
        var reached = MeasuredThroughAParent.Reach(Lists(), new HashSet<Guid> { ShoppingId });

        // The one row in the reported screenshot that was rightly unticked.
        Assert.False(reached.ContainsKey(DoctorId));
    }

    [Fact]
    public void The_measured_list_itself_is_left_out()
    {
        var reached = MeasuredThroughAParent.Reach(Lists(), new HashSet<Guid> { ShoppingId });

        // It stands on its own tie, and the checklist already draws it ticked and untickable-by-hand.
        Assert.False(reached.ContainsKey(ShoppingId));
    }

    [Fact]
    public void A_list_measured_in_its_own_right_is_left_out_even_when_a_group_also_gathers_it()
    {
        var reached = MeasuredThroughAParent.Reach(Lists(), new HashSet<Guid> { ShoppingId, RecipesId });

        // Ticking it here is what holds it, so the row must stay untickable rather than becoming a
        // consequence of the group above it.
        Assert.False(reached.ContainsKey(RecipesId));
        Assert.Equal(ShoppingId, reached[PastaId]);
    }

    [Fact]
    public void A_loop_between_gathered_lists_ends_the_branch_rather_than_the_walk()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var lists = new List<TaskDto>
        {
            List(ShoppingId, "Shopping", Gathers(first)),
            List(first, "First", Gathers(second)),
            List(second, "Second", Gathers(first))
        };

        var reached = MeasuredThroughAParent.Reach(lists, new HashSet<Guid> { ShoppingId });

        Assert.Equal(ShoppingId, reached[first]);
        Assert.Equal(ShoppingId, reached[second]);
    }

    [Fact]
    public void A_link_to_a_list_this_reader_cannot_see_ends_that_branch()
    {
        var missing = Guid.NewGuid();
        var lists = new List<TaskDto> { List(ShoppingId, "Shopping", Gathers(missing)) };

        var reached = MeasuredThroughAParent.Reach(lists, new HashSet<Guid> { ShoppingId });

        // Named, because the list above it says it gathers that id - but nothing is followed out of it,
        // and there is no row to draw for a list that was not given to us.
        Assert.Equal(ShoppingId, reached[missing]);
    }

    /// <summary>Shopping gathers Recipes, Recipes gathers Pasta, and Doctor stands on its own.</summary>
    private static IReadOnlyList<TaskDto> Lists()
        =>
        [
            List(ShoppingId, "Shopping", Gathers(RecipesId)),
            List(RecipesId, "Recipes", Gathers(PastaId)),
            List(PastaId, "Pasta"),
            List(DoctorId, "Doctor")
        ];

    private static TaskDto List(Guid id, string title, params TaskItemDto[] items)
        => new(
            id, title, items, IsCompleted: false, IsGroup: false, IsPrivate: false, EncryptedContent: null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);

    private static TaskItemDto Gathers(Guid taskListId)
        => new(
            Guid.NewGuid(), "Stands for another list", DueDateUtc: null, IsCompleted: false, taskListId,
            OverdueNotificationChannel: "None", RemindDaily: false,
            DailyReminderNotificationChannel: "None", DailyReminderTimeOfDay: new TimeOnly(9, 0));
}
