using Orbit.Core.Notifications;

namespace Orbit.Core.Inventory;

/// <summary>
/// A single product tracked in a user's stock, owned by exactly one user for its entire lifetime - no
/// sharing/locking concept, unlike Note/TaskList/CalendarEvent, since that was never requested and
/// would be pure scope creep on top of an already large feature.
/// </summary>
public sealed class InventoryItem
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; }
    public string ProductType { get; private set; }
    public string Category { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal? MinimumQuantity { get; private set; }
    public DateTimeOffset? ExpiryDate { get; private set; }

    /// <summary>Which channel(s), if any, warn the owner as ExpiryDate approaches - see InventoryExpiryReminderScheduler.</summary>
    public NotificationChannel ExpiryNotificationChannel { get; private set; }

    /// <summary>
    /// The system-managed restock TaskList/TaskItem currently open for this product, if any - see
    /// InventoryTaskListCoordinator/PendingRestockTaskResolver. Both null when quantity is above
    /// minimum, or no restock task has been created yet.
    /// </summary>
    public Guid? PendingRestockTaskListId { get; private set; }

    public Guid? PendingRestockTaskItemId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Whether this item is at or below its own minimum - always false when no minimum is set.</summary>
    public bool IsBelowMinimum => MinimumQuantity is { } minimumQuantity && Quantity <= minimumQuantity;

    private InventoryItem(
        Guid id, Guid userId, string name, string productType, string category, decimal quantity, decimal? minimumQuantity,
        DateTimeOffset? expiryDate, NotificationChannel expiryNotificationChannel, Guid? pendingRestockTaskListId,
        Guid? pendingRestockTaskItemId, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
    {
        Id = id;
        UserId = userId;
        Name = name;
        ProductType = productType;
        Category = category;
        Quantity = quantity;
        MinimumQuantity = minimumQuantity;
        ExpiryDate = expiryDate;
        ExpiryNotificationChannel = expiryNotificationChannel;
        PendingRestockTaskListId = pendingRestockTaskListId;
        PendingRestockTaskItemId = pendingRestockTaskItemId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static InventoryItem Create(
        Guid userId, string name, string productType, string category, decimal quantity, decimal? minimumQuantity,
        DateTimeOffset? expiryDate, NotificationChannel expiryNotificationChannel)
    {
        var now = DateTimeOffset.UtcNow;
        return new InventoryItem(
            Guid.NewGuid(), userId, name, productType, category, quantity, minimumQuantity, expiryDate, expiryNotificationChannel,
            pendingRestockTaskListId: null, pendingRestockTaskItemId: null, now, now);
    }

    /// <summary>Rebuilds an inventory item from already-persisted values, bypassing creation rules.</summary>
    public static InventoryItem FromPersistence(
        Guid id, Guid userId, string name, string productType, string category, decimal quantity, decimal? minimumQuantity,
        DateTimeOffset? expiryDate, NotificationChannel expiryNotificationChannel, Guid? pendingRestockTaskListId,
        Guid? pendingRestockTaskItemId, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
        => new(
            id, userId, name, productType, category, quantity, minimumQuantity, expiryDate, expiryNotificationChannel,
            pendingRestockTaskListId, pendingRestockTaskItemId, createdAtUtc, updatedAtUtc);

    public void Update(
        string name, string productType, string category, decimal quantity, decimal? minimumQuantity,
        DateTimeOffset? expiryDate, NotificationChannel expiryNotificationChannel)
    {
        Name = name;
        ProductType = productType;
        Category = category;
        Quantity = quantity;
        MinimumQuantity = minimumQuantity;
        ExpiryDate = expiryDate;
        ExpiryNotificationChannel = expiryNotificationChannel;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void SetPendingRestockTask(Guid taskListId, Guid taskItemId)
    {
        PendingRestockTaskListId = taskListId;
        PendingRestockTaskItemId = taskItemId;
    }

    public void ClearPendingRestockTask()
    {
        PendingRestockTaskListId = null;
        PendingRestockTaskItemId = null;
    }
}
