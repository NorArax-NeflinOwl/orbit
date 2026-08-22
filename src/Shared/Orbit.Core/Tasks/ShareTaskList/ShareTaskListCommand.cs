using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.ShareTaskList;

/// <summary>Returns null instead of a share id when taskListId doesn't exist or isn't owned by ownerUserId.</summary>
[ClientAction(ClientActionCategory.ShareElement)]
public sealed record ShareTaskListCommand(
    Guid OwnerUserId, Guid TaskListId, Guid RecipientUserId, ShareAccessLevel AccessLevel = ShareAccessLevel.ReadOnly)
    : IRequest<Guid?>;
