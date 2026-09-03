using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.GetInventoryShareStatus;

/// <summary>Returns null when shareId doesn't exist or wasn't offered to recipientUserId, otherwise whether it's been accepted.</summary>
public sealed record GetInventoryShareStatusQuery(Guid RecipientUserId, Guid ShareId) : IRequest<bool?>;
