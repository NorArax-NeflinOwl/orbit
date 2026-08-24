using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;

namespace Orbit.Core.Inventory.UpdateInventoryItem;

[ClientAction(ClientActionCategory.Edit)]
public sealed record UpdateInventoryItemCommand(
    Guid UserId, Guid Id, string Name, string ProductType, string Category, decimal Quantity, decimal? MinimumQuantity,
    DateTimeOffset? ExpiryDate, NotificationChannel ExpiryNotificationChannel) : IRequest<EditOutcome>;
