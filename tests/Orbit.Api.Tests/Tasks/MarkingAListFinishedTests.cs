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
/// Since 2026-09-09 there is a third answer, for the case the other two could not say either: every
/// entry ticked off and the list still open - see TaskListCompletion.Unfinished. What these pin is that
/// none of the three blocks the others.
/// </summary>
public sealed class MarkingAListFinishedTests
{
    [Fact]
    public void A_list_with_work_still_on_it_is_finished_once_its_owner_says_so()
    {
        var taskList = AListWith(Open("Paint the shed"), Open("Buy screws"));

        Assert.False(taskList.IsCompleted);
        taskList.SetCompletion(TaskListCompletion.Finished);

        Assert.True(taskList.IsCompleted);
        Assert.Equal(TaskListStatus.Completed, taskList.Status);
    }

    /// <summary>
    /// The answer the box could not give before. Unticking it on a list whose entries are all done used
    /// to hand the question straight back to the entries, which said "finished" again and put the tick
    /// back - so a list whose work was done but which was not itself finished had no way to say so.
    /// </summary>
    [Fact]
    public void A_list_whose_entries_are_all_done_can_still_be_said_to_be_unfinished()
    {
        var taskList = AListWith(Done("Paint the shed"));
        Assert.True(taskList.IsCompleted);

        taskList.SetCompletion(TaskListCompletion.Unfinished);

        Assert.False(taskList.IsCompleted);
        Assert.Equal(TaskListStatus.Incomplete, taskList.Status);
    }

    /// <summary>
    /// And with work left it reads as what it is rather than as Incomplete: that status is only ever
    /// about the gap between the entries and the list, and there is no gap while entries are open.
    /// </summary>
    [Fact]
    public void A_list_said_to_be_unfinished_with_work_left_reads_as_the_work_says()
    {
        var taskList = AListWith(Done("Paint the shed"), Open("Buy screws"));

        taskList.SetCompletion(TaskListCompletion.Unfinished);

        Assert.False(taskList.IsCompleted);
        Assert.Equal(TaskListStatus.Pending, taskList.Status);
    }

    /// <summary>Handing the question back to the entries is its own answer, and leaves a finished list finished.</summary>
    [Fact]
    public void Handing_the_question_back_to_the_entries_leaves_a_finished_list_finished()
    {
        var taskList = AListWith(Done("Paint the shed"));
        taskList.SetCompletion(TaskListCompletion.Finished);

        taskList.SetCompletion(TaskListCompletion.FromTheEntries);

        Assert.True(taskList.IsCompleted);
    }

    /// <summary>And on a list with work left, handing it back reopens it - which is what taking it back means.</summary>
    [Fact]
    public void Handing_the_question_back_on_a_list_with_work_left_reopens_it()
    {
        var taskList = AListWith(Open("Paint the shed"));
        taskList.SetCompletion(TaskListCompletion.Finished);

        taskList.SetCompletion(TaskListCompletion.FromTheEntries);

        Assert.False(taskList.IsCompleted);
        Assert.Equal(TaskListStatus.New, taskList.Status);
    }

    /// <summary>
    /// Ticking every entry still finishes a list nobody answered for. This is the half that existed
    /// before and must go on working: the reader's answer is a second way to the same state, not a
    /// replacement.
    /// </summary>
    [Fact]
    public void Ticking_every_entry_still_finishes_a_list_nobody_answered_for()
    {
        var taskList = AListWith(Done("Paint the shed"), Done("Buy screws"));

        Assert.True(taskList.IsCompleted);
        Assert.Equal(TaskListCompletion.FromTheEntries, taskList.Completion);
        Assert.Equal(TaskListStatus.Completed, taskList.Status);
    }

    /// <summary>
    /// An empty list nobody answered for is not finished - it is new. Somebody who says it is has still
    /// said something, and that stands: an empty list can be a list of things that turned out not to be
    /// needed.
    /// </summary>
    [Fact]
    public void An_empty_list_is_finished_only_if_somebody_says_so()
    {
        var taskList = AListWith();
        Assert.False(taskList.IsCompleted);

        taskList.SetCompletion(TaskListCompletion.Finished);

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
        taskList.SetCompletion(TaskListCompletion.Finished);

        taskList.Update(
            "Shed", [Done("Paint the shed"), Open("Creosote the fence")], isGroup: false, isPrivate: false,
            encryptedContent: null, ItemPriority.Normal);

        Assert.True(taskList.IsCompleted);
        Assert.Equal(TaskListCompletion.Finished, taskList.Completion);
    }

    /// <summary>Saying again what is already stored changes nothing, so the list is not stamped as touched.</summary>
    [Fact]
    public void Saying_it_again_does_not_touch_the_list()
    {
        var taskList = AListWith(Open("Paint the shed"));
        taskList.SetCompletion(TaskListCompletion.Finished);
        var updatedBefore = taskList.UpdatedAtUtc;

        Assert.False(taskList.SetCompletion(TaskListCompletion.Finished));
        Assert.Equal(updatedBefore, taskList.UpdatedAtUtc);
    }

    private static TaskList AListWith(params TaskItem[] items)
        => TaskList.Create(Guid.NewGuid(), "Shed", items);

    private static TaskItem Open(string description) => TaskItem.Create(description, null, isCompleted: false);

    private static TaskItem Done(string description) => TaskItem.Create(description, null, isCompleted: true);
}
