using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.ShareTaskList;

/// <summary>Returns null under the same conditions as Orbit.Core.Notes.ShareNote.ShareNoteCommand - see its comment.</summary>
[ClientAction(ClientActionCategory.ShareElement)]
public sealed record ShareTaskListCommand(
    Guid OwnerUserId, Guid TaskListId, Guid RecipientUserId, ShareAccessLevel AccessLevel = ShareAccessLevel.ReadOnly)
    : IRequest<ShareOutcome?>;
