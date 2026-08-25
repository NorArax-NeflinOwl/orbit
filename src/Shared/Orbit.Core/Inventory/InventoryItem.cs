using Orbit.Core.Notifications;

namespace Orbit.Core.Inventory;

/// <summary>
/// A single product tracked in stock. Belongs to a <see cref="Warehouse"/> rather than directly to a
/// user: who may read or change this item is entirely decided by who may read or change its warehouse
/// (see WarehouseAccessResolver), so an item carries no owner and no access level of its own. Has no
/// edit-lock concept, unlike Note/TaskList/CalendarEvent - see Warehouse's class comment.
/// </summary>
public sealed class InventoryItem
{
    public Guid Id { get; private set; }
    public Guid WarehouseId { get; private set; }
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

    /// <summary>
    /// Whether this item has dropped strictly below its own minimum - always false when no minimum is
    /// set. Sitting exactly at the minimum counts as fine: the minimum is the level to keep, not the
    /// level that already needs restocking, so 1 of 1 raises no task.
    /// </summary>
    public bool IsBelowMinimum => MinimumQuantity is { } minimumQuantity && Quantity < minimumQuantity;

    private InventoryItem(
        Guid id, Guid warehouseId, string name, string productType, string category, decimal quantity, decimal? minimumQuantity,
        DateTimeOffset? expiryDate, NotificationChannel expiryNotificationChannel, Guid? pendingRestockTaskListId,
        Guid? pendingRestockTaskItemId, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
    {
        Id = id;
        WarehouseId = warehouseId;
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
        Guid warehouseId, string name, string productType, string category, decimal quantity, decimal? minimumQuantity,
        DateTimeOffset? expiryDate, NotificationChannel expiryNotificationChannel)
    {
        var now = DateTimeOffset.UtcNow;
        return new InventoryItem(
            Guid.NewGuid(), warehouseId, name, productType, category, quantity, minimumQuantity, expiryDate, expiryNotificationChannel,
            pendingRestockTaskListId: null, pendingRestockTaskItemId: null, now, now);
    }

    /// <summary>Rebuilds an inventory item from already-persisted values, bypassing creation rules.</summary>
    public static InventoryItem FromPersistence(
        Guid id, Guid warehouseId, string name, string productType, string category, decimal quantity, decimal? minimumQuantity,
        DateTimeOffset? expiryDate, NotificationChannel expiryNotificationChannel, Guid? pendingRestockTaskListId,
        Guid? pendingRestockTaskItemId, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
        => new(
            id, warehouseId, name, productType, category, quantity, minimumQuantity, expiryDate, expiryNotificationChannel,
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
