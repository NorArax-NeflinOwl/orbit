using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.AcceptTaskListShare;

/// <summary>Returns false when shareId doesn't exist, wasn't offered to recipientUserId, or its source task list is gone.</summary>
public sealed record AcceptTaskListShareCommand(Guid RecipientUserId, Guid ShareId) : IRequest<bool>;
