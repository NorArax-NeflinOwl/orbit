using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.ArchiveTaskList;

/// <summary>
/// Puts one list away, or brings it back - see TaskList.Archive, and
/// Orbit.Core.Folders.BuiltInFolder.Archived, which is the tab it gathers under while it is away.
///
/// Its own command rather than a field on the update, for the reason MoveTaskListToFolderCommand is its
/// own: an update replaces the whole thing, so a client that had never heard of archiving would bring
/// back everything its owner had put away, every time it saved.
///
/// One command for both directions rather than two. Putting away and bringing back are the same
/// decision answered differently, and a pair would be two handlers agreeing about who may - which is
/// how a rule comes to hold on one of them and not the other. SetNotePinnedCommand takes its bool the
/// same way.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record ArchiveTaskListCommand(Guid UserId, Guid TaskListId, bool IsArchived) : IRequest<bool>;
