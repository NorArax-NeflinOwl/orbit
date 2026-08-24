using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;

namespace Orbit.Core.Inventory.CreateInventoryItem;

[ClientAction(ClientActionCategory.Save)]
public sealed record CreateInventoryItemCommand(
    Guid UserId, string Name, string ProductType, string Category, decimal Quantity, decimal? MinimumQuantity,
    DateTimeOffset? ExpiryDate, NotificationChannel ExpiryNotificationChannel) : IRequest<Guid>;
