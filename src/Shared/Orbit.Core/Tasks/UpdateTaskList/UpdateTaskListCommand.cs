using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.UpdateTaskList;

[ClientAction(ClientActionCategory.Edit)]
public sealed record UpdateTaskListCommand(
    Guid UserId, Guid Id, string Title, IReadOnlyList<TaskItem> Items, bool IsGroup, bool IsPrivate, EncryptedPayload? EncryptedContent,
    ItemPriority Priority = ItemPriority.Normal,
    /// <summary>
    /// Null leaves the stored description alone - see UpdateTaskRequest. An empty string clears it.
    /// </summary>
    string? Description = null)
    : IRequest<EditOutcome>;
