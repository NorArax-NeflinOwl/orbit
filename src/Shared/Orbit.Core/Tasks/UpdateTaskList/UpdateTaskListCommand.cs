using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.UpdateTaskList;

/// <summary>
/// <paramref name="Location"/> is only kept for a calendar list - see TaskList.SetKind.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record UpdateTaskListCommand(
    Guid UserId, Guid Id, string Title, IReadOnlyList<TaskItem> Items, bool IsGroup, bool IsPrivate, EncryptedPayload? EncryptedContent,
    ItemPriority Priority = ItemPriority.Normal, TaskListKind Kind = TaskListKind.Checklist, string Location = "")
    : IRequest<EditOutcome>;
