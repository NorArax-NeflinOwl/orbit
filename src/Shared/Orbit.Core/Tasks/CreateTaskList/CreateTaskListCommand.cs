using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.CreateTaskList;

[ClientAction(ClientActionCategory.Save)]
public sealed record CreateTaskListCommand(
    Guid UserId, string Title, IReadOnlyList<TaskItem> Items, bool IsGroup, bool IsPrivate, EncryptedPayload? EncryptedContent,
    ItemPriority Priority = ItemPriority.Normal,
    string? Description = null,
    /// <summary>Where to file it, or null for the built-in folder - see Orbit.Core.Folders.BuiltInFolder.</summary>
    Guid? FolderId = null,
    /// <summary>What the reader has said about it being finished - see TaskListCompletion.</summary>
    TaskListCompletion Completion = TaskListCompletion.FromTheEntries,
    /// <summary>The words it is tagged with - see TaskList.Tags.</summary>
    IReadOnlyList<string>? Tags = null)
    : IRequest<Guid>;
