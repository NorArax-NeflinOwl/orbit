using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// The order the work on one list has to be done in - "hang the door" waiting on "fit the hinges".
/// The rule is applied wherever a list is built or saved, so nothing that reaches storage can break it.
/// </summary>
public sealed class TaskListStepsTests
{
    [Fact]
    public void An_entry_waiting_on_unfinished_work_cannot_be_ticked()
    {
        var hinges = Entry("Fit the hinges");
        var door = Entry("Hang the door", isCompleted: true, waitsFor: [hinges.Id]);

        TaskListSteps.Apply([hinges, door]);

        Assert.False(door.IsCompleted);
    }

    [Fact]
    public void And_can_be_ticked_once_the_step_is_done()
    {
        var hinges = Entry("Fit the hinges", isCompleted: true);
        var door = Entry("Hang the door", isCompleted: true, waitsFor: [hinges.Id]);

        TaskListSteps.Apply([hinges, door]);

        Assert.True(door.IsCompleted);
    }

    /// <summary>
    /// A step somebody gave up on says the work was not done, which is the one thing the waiting entry
    /// needs to be true - so it blocks exactly as an untouched one does.
    /// </summary>
    [Fact]
    public void A_step_that_was_crossed_out_blocks_too()
    {
        var hinges = Entry("Fit the hinges", isFailed: true);
        var door = Entry("Hang the door", isCompleted: true, waitsFor: [hinges.Id]);

        TaskListSteps.Apply([hinges, door]);

        Assert.False(door.IsCompleted);
    }

    /// <summary>
    /// The block is about finishing the entry, not about giving up on it: somebody held up by a step
    /// that is never going to happen has to be able to close their list.
    /// </summary>
    [Fact]
    public void A_waiting_entry_can_still_be_crossed_out()
    {
        var hinges = Entry("Fit the hinges");
        var door = Entry("Hang the door", isFailed: true, waitsFor: [hinges.Id]);

        TaskListSteps.Apply([hinges, door]);

        Assert.True(door.IsFailed);
    }

    /// <summary>
    /// Ids arrive from clients and outlive the entries they name - a step deleted in the same save, an
    /// id from another list. An entry waiting for something nobody can see could never be crossed off.
    /// </summary>
    [Fact]
    public void A_step_that_is_not_an_entry_here_is_dropped()
    {
        var door = Entry("Hang the door", isCompleted: true, waitsFor: [Guid.NewGuid()]);

        TaskListSteps.Apply([door]);

        Assert.Empty(door.WaitsForTaskItemIds);
        Assert.True(door.IsCompleted);
    }

    /// <summary>An entry cannot wait for itself: that is a step that could never be taken.</summary>
    [Fact]
    public void An_entry_does_not_wait_for_itself()
    {
        var door = TaskItem.Create("Hang the door", dueDateUtc: null, isCompleted: false);

        var waitingOnItself = TaskItem.FromPersistence(
            door.Id, door.Description, null, isCompleted: true, linkedTaskListIds: null, reminders: null,
            waitsForTaskItemIds: [door.Id]);

        Assert.Empty(waitingOnItself.WaitsForTaskItemIds);
    }

    /// <summary>What a screen names when it refuses the tick - only what is actually still outstanding.</summary>
    [Fact]
    public void What_it_is_waiting_on_names_the_unfinished_steps()
    {
        var hinges = Entry("Fit the hinges", isCompleted: true);
        var frame = Entry("Level the frame");
        var door = Entry("Hang the door", waitsFor: [hinges.Id, frame.Id]);

        var waiting = TaskListSteps.WhatItIsWaitingOn(door, [hinges, frame, door]);

        Assert.Equal(["Level the frame"], waiting.Select(step => step.Description));
    }

    private static TaskItem Entry(
        string description, bool isCompleted = false, bool isFailed = false, IReadOnlyList<Guid>? waitsFor = null)
        => TaskItem.Create(
            description, dueDateUtc: null, isCompleted, linkedTaskListIds: null, reminders: null, subject: null,
            categories: null, product: null, notes: null, isFailed, waitsFor);
}
