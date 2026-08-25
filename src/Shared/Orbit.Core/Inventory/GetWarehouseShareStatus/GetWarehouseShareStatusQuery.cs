using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.GetWarehouseShareStatus;

/// <summary>Returns null when shareId doesn't exist or wasn't offered to recipientUserId, otherwise whether it's been accepted.</summary>
public sealed record GetWarehouseShareStatusQuery(Guid RecipientUserId, Guid ShareId) : IRequest<bool?>;
