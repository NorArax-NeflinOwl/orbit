using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.CreateTaskList;

/// <summary>
/// <paramref name="Location"/> is only kept for a calendar list - see TaskList.SetKind.
/// </summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record CreateTaskListCommand(
    Guid UserId, string Title, IReadOnlyList<TaskItem> Items, bool IsGroup, bool IsPrivate, EncryptedPayload? EncryptedContent,
    ItemPriority Priority = ItemPriority.Normal, TaskListKind Kind = TaskListKind.Checklist, string Location = "")
    : IRequest<Guid>;
