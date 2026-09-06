using Orbit.Contracts.Tasks;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Tasks;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The one line a list's row shows: what is still to be done. Orbit.Web's cards say the same thing, and
/// got it wrong the same way first - a group's rows only point at other lists, and skipping them said
/// "Nothing left to do." over every one of its members' open errands.
/// </summary>
public sealed class TaskListRowTests
{
    [Fact]
    public void A_list_names_its_first_unfinished_entry()
    {
        var list = List("Groceries", Done("Buy milk"), Open("Buy bread"));

        var row = Show(list, [list]);

        Assert.Equal("Buy bread", row.NextThing);
        Assert.False(row.IsNextThingOnAnotherList);
    }

    [Fact]
    public void A_group_list_names_the_work_on_the_list_it_points_at()
    {
        var member = List("Kitchen", Open("Fix the tap"));
        var group = List("House", PointsAt(member, "Kitchen"));

        var row = Show(group, [group, member]);

        Assert.Equal("Fix the tap", row.NextThing);
        Assert.Equal("Kitchen", row.NextThingOnList);
    }

    /// <summary>A member whose every errand is done is passed over, not reported as the answer.</summary>
    [Fact]
    public void A_group_list_looks_past_a_member_with_nothing_left()
    {
        var finished = List("Kitchen", Done("Fix the tap"));
        var busy = List("Garden", Open("Mow the lawn"));
        var group = List("House", PointsAt(finished, "Kitchen"), PointsAt(busy, "Garden"));

        var row = Show(group, [group, finished, busy]);

        Assert.Equal("Mow the lawn", row.NextThing);
        Assert.Equal("Garden", row.NextThingOnList);
    }

    /// <summary>
    /// A member this phone has not synced, or one the reader cannot see: the pointing row's own name is
    /// that list's title, which still says more than nothing.
    /// </summary>
    [Fact]
    public void A_link_to_a_list_this_phone_does_not_hold_falls_back_to_its_own_name()
    {
        var group = List("House", Link("Kitchen", Guid.NewGuid()));

        var row = Show(group, [group]);

        Assert.Equal("Kitchen", row.NextThing);
        Assert.False(row.IsNextThingOnAnotherList);
    }

    [Fact]
    public void A_finished_list_says_there_is_nothing_left()
    {
        var list = List("Groceries", Done("Buy milk"));

        var row = Show(list, [list]);

        Assert.False(row.HasNextThing);
        Assert.True(row.HasNothingLeftToDo);
    }

    /// <summary>An empty list is not finished, it is empty - and saying it is done would be a lie.</summary>
    [Fact]
    public void An_empty_list_says_neither()
    {
        var list = List("Groceries");

        var row = Show(list, [list]);

        Assert.False(row.HasNextThing);
        Assert.False(row.HasNothingLeftToDo);
    }

    /// <summary>
    /// Only the owner may pin, and the server turns anybody else down
    /// (SetTaskListPinnedCommandHandler) - so the control has to be left out rather than offered and
    /// refused. A recipient tapping it got no pin, no message and no reason. The same rule
    /// NoteListItem.CanBePinned already followed.
    /// </summary>
    [Fact]
    public void A_list_shared_with_this_reader_offers_no_pin()
    {
        var theirs = List("Groceries", Open("Buy bread"));
        theirs.IsShared = true;

        Assert.False(Show(theirs, [theirs]).CanBePinned);
    }

    [Fact]
    public void A_list_of_this_readers_own_offers_one()
    {
        var mine = List("Groceries", Open("Buy bread"));

        Assert.True(Show(mine, [mine]).CanBePinned);
    }

    private static TaskListRow Show(LocalTaskList taskList, IReadOnlyList<LocalTaskList> everyList)
        => TaskListRow.From(
            taskList, everyList, hasUnsentChanges: false, FixedNetworkStatus.Online,
            new Translations(new InMemoryLanguageStore()));

    private static LocalTaskList List(string title, params TaskItemDto[] items)
        => new()
        {
            LocalId = Guid.NewGuid(),
            ServerId = Guid.NewGuid(),
            Title = title,
            Items = items,
            CreatedAtUtc = DateTimeOffset.Parse("2026-06-01T00:00:00Z")
        };

    private static TaskItemDto Open(string description) => Entry(description, isCompleted: false, null);

    private static TaskItemDto Done(string description) => Entry(description, isCompleted: true, null);

    /// <summary>A row that is nothing but a pointer at another list, as a group list's rows all are.</summary>
    private static TaskItemDto PointsAt(LocalTaskList member, string named)
        => Link(named, member.ServerId!.Value);

    private static TaskItemDto Link(string description, Guid linkedTaskListId)
        => Entry(description, isCompleted: false, linkedTaskListId);

    private static TaskItemDto Entry(string description, bool isCompleted, Guid? linkedTaskListId)
        => new(
            Guid.NewGuid(), description, null, isCompleted, linkedTaskListId, "None", false, "None",
            new TimeOnly(9, 0));
}
