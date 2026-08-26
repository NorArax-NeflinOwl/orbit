using Orbit.Contracts.Tasks;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;

namespace Orbit.Maui.Features.Tasks;

/// <summary>
/// One row of the task lists screen - the task-list counterpart of NoteListItem, and shaped the same
/// way: what to show, plus the two things the user has to be told about it.
/// </summary>
public sealed record TaskListRow(
	Guid LocalId, string Title, int ItemCount, int CompletedCount, bool IsPinned,
	DateTimeOffset UpdatedAtUtc, bool HasUnsentChanges, OfflineEditRefusal Refusal)
{
	public static TaskListRow From(LocalTaskList taskList, bool hasUnsentChanges, INetworkStatus networkStatus)
		=> new(
			taskList.LocalId, taskList.Title, taskList.Items.Count,
			taskList.Items.Count(item => item.IsCompleted), taskList.IsPinned, taskList.UpdatedAtUtc,
			hasUnsentChanges, OfflineEditPolicy.Evaluate(taskList, networkStatus));

	public bool IsEditable => Refusal is OfflineEditRefusal.None;

	public string Progress => ItemCount == 0 ? "No items yet" : $"{CompletedCount} of {ItemCount} done";

	/// <summary>Empty when there is nothing worth saying, which is the common case.</summary>
	public string Status => Refusal switch
	{
		OfflineEditRefusal.SharedWithYou => "Shared with you - read-only until you're back online",
		OfflineEditRefusal.SharedWithOthers => "Shared with others - read-only until you're back online",
		_ => HasUnsentChanges ? "Waiting to sync" : string.Empty
	};

	public bool HasStatus => Status.Length > 0;

	/// <summary>A new list starts empty; items are added by editing it.</summary>
	public static IReadOnlyList<TaskItemDto> NoItems => [];
}
