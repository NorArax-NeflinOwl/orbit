using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;

namespace Orbit.Core.Inventory.CreateInventoryItem;

/// <summary>Returns null when the caller can't write to WarehouseId - it doesn't exist, isn't shared with them, or their grant is read-only.</summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record CreateInventoryItemCommand(
    Guid UserId, Guid WarehouseId, string Name, string ProductType, string Category, decimal Quantity, decimal? MinimumQuantity,
    DateTimeOffset? ExpiryDate, NotificationChannel ExpiryNotificationChannel) : IRequest<Guid?>;
