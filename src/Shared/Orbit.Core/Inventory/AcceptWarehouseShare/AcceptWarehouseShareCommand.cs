using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.AcceptWarehouseShare;

/// <summary>Returns false when shareId doesn't exist or wasn't offered to recipientUserId.</summary>
public sealed record AcceptWarehouseShareCommand(Guid RecipientUserId, Guid ShareId) : IRequest<bool>;
