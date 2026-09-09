namespace Orbit.Core.Tasks;

/// <summary>
/// The order the work on one list has to be done in - "hang the door" waiting on "fit the hinges".
/// An entry names the entries of its own list it waits for (see <see cref="TaskItem.WaitsForTaskItemIds"/>),
/// and this is where that means anything.
///
/// Two rules, applied wherever a list is built or saved:
///
/// <list type="bullet">
///   <item>A step that is not an entry on this list is dropped. Ids arrive from clients and outlive
///     the entries they name - a step deleted in the same save, an id from another list - and an
///     entry waiting for something nobody can see would be an entry nobody could ever cross off.</item>
///   <item><b>An entry waiting on unfinished work cannot be ticked.</b> Not refused with an error:
///     the tick is taken back, the same way a linked entry's completion is ignored rather than briefly
///     believed (see <see cref="TaskItem.Create"/>). Only the tick - crossing the entry out is giving
///     up on it, which is exactly what somebody blocked by a step that will never happen needs.</item>
/// </list>
///
/// A step that was crossed out blocks too: what it says is that the work was not done. The whole point
/// of the field is that this entry cannot honestly be called finished until that one was.
/// </summary>
public static class TaskListSteps
{
    /// <summary>
    /// Applies both rules to a list's entries, in place. Called where a list is made and where one is
    /// saved, so nothing that reaches storage can break them.
    /// </summary>
    public static void Apply(IReadOnlyList<TaskItem> items)
    {
        var idsOnThisList = items.Select(item => item.Id).ToHashSet();
        foreach (var item in items)
        {
            item.WaitsOnlyFor(idsOnThisList);
        }

        var byId = items.ToDictionary(item => item.Id);
        foreach (var item in items.Where(item => item.IsCompleted && item.WaitsForAnything))
        {
            if (!IsClearToStart(item, byId))
            {
                item.CannotBeDoneYet();
            }
        }
    }

    /// <summary>
    /// Whether everything this entry waits for is done. Asked of <see cref="TaskItem.IsCompleted"/>
    /// rather than of IsResolved: a step somebody gave up on is finished with, and still says the work
    /// was not done - which is the one thing this entry is waiting to be true.
    ///
    /// A step whose own steps are unfinished counts as unfinished itself, however deep that goes: it
    /// cannot be ticked either, so what it says about itself is already the answer.
    /// </summary>
    public static bool IsClearToStart(TaskItem item, IReadOnlyDictionary<Guid, TaskItem> byId)
        => item.WaitsForTaskItemIds.All(
            waitedFor => byId.TryGetValue(waitedFor, out var step) && step.IsCompleted);

    /// <summary>
    /// The entries this one is still waiting on, for a screen to name - empty when it is clear to
    /// start, which is what every entry that waits for nothing always is.
    /// </summary>
    public static IReadOnlyList<TaskItem> WhatItIsWaitingOn(TaskItem item, IReadOnlyList<TaskItem> items)
    {
        var byId = items.ToDictionary(candidate => candidate.Id);
        return
        [
            .. item.WaitsForTaskItemIds
                .Select(waitedFor => byId.GetValueOrDefault(waitedFor))
                .OfType<TaskItem>()
                .Where(step => !step.IsCompleted)
        ];
    }
}
