using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.AcceptInventoryShare;

/// <summary>Returns false when shareId doesn't exist or wasn't offered to recipientUserId.</summary>
public sealed record AcceptInventoryShareCommand(Guid RecipientUserId, Guid ShareId) : IRequest<bool>;
