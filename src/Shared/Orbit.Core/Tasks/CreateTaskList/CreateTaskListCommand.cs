using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.CreateTaskList;

[ClientAction(ClientActionCategory.Save)]
public sealed record CreateTaskListCommand(
    Guid UserId, string Title, IReadOnlyList<TaskItem> Items, bool IsGroup, bool IsPrivate, EncryptedPayload? EncryptedContent,
    ItemPriority Priority = ItemPriority.Normal,
    string? Description = null,
    /// <summary>Where to file it, or null for the built-in folder - see Orbit.Core.Folders.BuiltInFolder.</summary>
    Guid? FolderId = null)
    : IRequest<Guid>;
