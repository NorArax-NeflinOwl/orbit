using Orbit.Contracts.Tasks;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// One row of the task lists screen - the task-list counterpart of NoteListItem, and shaped the same
/// way: what to show, plus the two things the user has to be told about it.
/// </summary>
/// <param name="Progress">Already in the reader's language, so the row itself needs no dictionary.</param>
/// <param name="State">
/// Where the list stands - the same four words the filter chips offer, so a row and the chip that would
/// show it never disagree.
/// </param>
/// <param name="NextThing">What is still to be done, empty when nothing is.</param>
/// <param name="NextThingOnList">
/// The member list that thing sits on, when it was found through a link - empty when it is the list's
/// own. Without it a group's row names an errand with no hint of where it came from.
/// </param>
public sealed record TaskListRow(
    Guid LocalId, string Title, int ItemCount, int CompletedCount, bool IsPinned,
    DateTimeOffset UpdatedAtUtc, bool HasUnsentChanges, OfflineEditRefusal Refusal,
    string Progress, string Status, string State, string NextThing, string NextThingOnList)
{
    /// <param name="everyList">
    /// What else the phone holds, so a row that only points at another list can be looked up on the list
    /// it points at rather than skipped.
    /// </param>
    public static TaskListRow From(
        LocalTaskList taskList, IReadOnlyList<LocalTaskList> everyList, bool hasUnsentChanges,
        INetworkStatus networkStatus, Translations translations)
    {
        var itemCount = taskList.Items.Count;
        var completedCount = taskList.Items.Count(item => item.IsCompleted);
        var refusal = OfflineEditPolicy.Evaluate(taskList, networkStatus);
        var next = NextThingLeftToDo(taskList, everyList);

        return new(
            taskList.LocalId, taskList.Title, itemCount, completedCount, taskList.IsPinned,
            taskList.UpdatedAtUtc, hasUnsentChanges, refusal,
            Describe(itemCount, completedCount, translations),
            OfflineEditExplanation.For(refusal, hasUnsentChanges, translations),
            TaskListView.Describe(taskList.Status, translations),
            next?.Description ?? string.Empty,
            next?.OnList ?? string.Empty);
    }

    public bool IsEditable => Refusal is OfflineEditRefusal.None;

    public bool HasStatus => Status.Length > 0;

    public bool HasNextThing => NextThing.Length > 0;

    public bool IsNextThingOnAnotherList => NextThingOnList.Length > 0;

    /// <summary>Said only for a list that has something on it - an empty one is not finished, it is empty.</summary>
    public bool HasNothingLeftToDo => NextThing.Length == 0 && ItemCount > 0;

    /// <summary>A new list starts empty; items are added by editing it.</summary>
    public static IReadOnlyList<TaskItemDto> NoItems => [];

    /// <summary>What is still to be done, and where it sits.</summary>
    private sealed record ThingLeftToDo(string Description, string? OnList);

    /// <summary>
    /// A row that only points at another list is not work itself, so what it stands for is looked up on
    /// the list it points at. A group list is nothing but such rows, and skipping them said "Nothing
    /// left to do." over every one of its members' open errands - the bug Orbit.Web fixed in its cards.
    /// </summary>
    private static ThingLeftToDo? NextThingLeftToDo(
        LocalTaskList taskList, IReadOnlyList<LocalTaskList> everyList)
    {
        foreach (var item in taskList.Items.Where(item => !item.IsCompleted))
        {
            if (item.LinkedTaskListId is not { } linkedTaskListId)
            {
                return new ThingLeftToDo(item.Description, OnList: null);
            }

            // A member list this reader cannot see, or one this phone has not synced: the row's own name
            // is that list's title, which still says more than nothing.
            var linked = everyList.FirstOrDefault(candidate => candidate.ServerId == linkedTaskListId);
            if (linked is null)
            {
                return new ThingLeftToDo(item.Description, OnList: null);
            }

            if (linked.Items.FirstOrDefault(candidate => !candidate.IsCompleted) is { } linkedItem)
            {
                return new ThingLeftToDo(linkedItem.Description, linked.Title);
            }
        }

        return null;
    }

    private static string Describe(int itemCount, int completedCount, Translations translations)
        => itemCount == 0
            ? translations["No items yet"]
            : translations.Format("Done: {0} of {1}", completedCount, itemCount);
}
