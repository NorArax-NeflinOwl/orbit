using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.CreateTaskList;

[ClientAction(ClientActionCategory.Save)]
public sealed record CreateTaskListCommand(Guid UserId, string Title, IReadOnlyList<TaskItem> Items, bool IsGroup, bool IsPrivate, EncryptedPayload? EncryptedContent) : IRequest<Guid>;
