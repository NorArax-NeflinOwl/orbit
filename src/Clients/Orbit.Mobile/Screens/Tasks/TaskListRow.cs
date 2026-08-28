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
public sealed record TaskListRow(
    Guid LocalId, string Title, int ItemCount, int CompletedCount, bool IsPinned,
    DateTimeOffset UpdatedAtUtc, bool HasUnsentChanges, OfflineEditRefusal Refusal,
    string Progress, string Status, bool IsShared = false)
{
    public static TaskListRow From(
        LocalTaskList taskList, bool hasUnsentChanges, INetworkStatus networkStatus, Translations translations)
    {
        var itemCount = taskList.Items.Count;
        var completedCount = taskList.Items.Count(item => item.IsCompleted);
        var refusal = OfflineEditPolicy.Evaluate(taskList, networkStatus);

        return new(
            taskList.LocalId, taskList.Title, itemCount, completedCount, taskList.IsPinned,
            taskList.UpdatedAtUtc, hasUnsentChanges, refusal,
            Describe(itemCount, completedCount, translations),
            OfflineEditExplanation.For(refusal, hasUnsentChanges, translations),
            taskList.IsShared);
    }

    /// <summary>
    /// Only the owner may pin - see SetTaskListPinnedCommandHandler - so a list shared with this user
    /// shows the state without offering to change it. Not gated on being online: no second writer.
    /// </summary>
    public bool CanBePinned => !IsShared;

    public bool IsEditable => Refusal is OfflineEditRefusal.None;

    public bool HasStatus => Status.Length > 0;

    /// <summary>A new list starts empty; items are added by editing it.</summary>
    public static IReadOnlyList<TaskItemDto> NoItems => [];

    private static string Describe(int itemCount, int completedCount, Translations translations)
        => itemCount == 0
            ? translations["No items yet"]
            : translations.Format("Done: {0} of {1}", completedCount, itemCount);
}
