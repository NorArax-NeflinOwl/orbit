using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.MoveTaskListToFolder;

/// <summary>Mirrors MoveNoteToFolderCommand - see it for why filing is its own command.</summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record MoveTaskListToFolderCommand(Guid UserId, Guid TaskListId, Guid? FolderId) : IRequest<bool>;
