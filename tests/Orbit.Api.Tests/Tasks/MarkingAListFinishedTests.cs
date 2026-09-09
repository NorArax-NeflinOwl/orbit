using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// A list is finished when every entry on it is ticked off - and, since 2026-09-08, when its owner says
/// so whatever is still written on it. The second is the ordinary case the first could not express: the
/// last two things stopped mattering, or were done somewhere else, and ticking them off would have been
/// a claim about the entries rather than about the list.
///
/// Both roads lead to the same state. What these pin is that neither road blocks the other.
/// </summary>
public sealed class MarkingAListFinishedTests
{
    [Fact]
    public void A_list_with_work_still_on_it_is_finished_once_its_owner_says_so()
    {
        var taskList = AListWith(Open("Paint the shed"), Open("Buy screws"));

        Assert.False(taskList.IsCompleted);
        taskList.SetMarkedCompleted(true);

        Assert.True(taskList.IsCompleted);
        Assert.Equal(TaskListStatus.Completed, taskList.Status);
    }

    /// <summary>Unmarking hands the question back to the entries rather than forcing "not done".</summary>
    [Fact]
    public void Unmarking_a_list_whose_entries_are_all_done_leaves_it_finished()
    {
        var taskList = AListWith(Done("Paint the shed"));
        taskList.SetMarkedCompleted(true);

        taskList.SetMarkedCompleted(false);

        Assert.True(taskList.IsCompleted);
    }

    /// <summary>And on a list with work left, unmarking reopens it - which is what taking it back means.</summary>
    [Fact]
    public void Unmarking_a_list_with_work_left_reopens_it()
    {
        var taskList = AListWith(Open("Paint the shed"));
        taskList.SetMarkedCompleted(true);

        taskList.SetMarkedCompleted(false);

        Assert.False(taskList.IsCompleted);
        Assert.Equal(TaskListStatus.New, taskList.Status);
    }

    /// <summary>
    /// Ticking every entry still finishes a list nobody marked. This is the half that existed before and
    /// must go on working: the new flag is a second way to the same state, not a replacement.
    /// </summary>
    [Fact]
    public void Ticking_every_entry_still_finishes_a_list_nobody_marked()
    {
        var taskList = AListWith(Done("Paint the shed"), Done("Buy screws"));

        Assert.True(taskList.IsCompleted);
        Assert.False(taskList.IsMarkedCompleted);
    }

    /// <summary>
    /// An empty list nobody marked is not finished - it is new. Somebody who marks it has still said
    /// something, and that stands: an empty list can be a list of things that turned out not to be
    /// needed.
    /// </summary>
    [Fact]
    public void An_empty_list_is_finished_only_if_somebody_says_so()
    {
        var taskList = AListWith();
        Assert.False(taskList.IsCompleted);

        taskList.SetMarkedCompleted(true);

        Assert.True(taskList.IsCompleted);
    }

    /// <summary>
    /// Adding an entry to a list somebody closed does not quietly reopen it. They said the *list* was
    /// done, not that its entries were, and an update rebuilds the entries - so this is the one place
    /// the two rules could have collided.
    /// </summary>
    [Fact]
    public void Adding_an_entry_to_a_finished_list_does_not_reopen_it()
    {
        var taskList = AListWith(Done("Paint the shed"));
        taskList.SetMarkedCompleted(true);

        taskList.Update(
            "Shed", [Done("Paint the shed"), Open("Creosote the fence")], isGroup: false, isPrivate: false,
            encryptedContent: null, ItemPriority.Normal);

        Assert.True(taskList.IsCompleted);
        Assert.True(taskList.IsMarkedCompleted);
    }

    /// <summary>Saying again what is already stored changes nothing, so the list is not stamped as touched.</summary>
    [Fact]
    public void Saying_it_again_does_not_touch_the_list()
    {
        var taskList = AListWith(Open("Paint the shed"));
        taskList.SetMarkedCompleted(true);
        var updatedBefore = taskList.UpdatedAtUtc;

        Assert.False(taskList.SetMarkedCompleted(true));
        Assert.Equal(updatedBefore, taskList.UpdatedAtUtc);
    }

    private static TaskList AListWith(params TaskItem[] items)
        => TaskList.Create(Guid.NewGuid(), "Shed", items);

    private static TaskItem Open(string description) => TaskItem.Create(description, null, isCompleted: false);

    private static TaskItem Done(string description) => TaskItem.Create(description, null, isCompleted: true);
}
