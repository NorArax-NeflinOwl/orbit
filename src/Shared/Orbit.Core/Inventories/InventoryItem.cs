using Orbit.Core;
using Orbit.Core.Notifications;

namespace Orbit.Core.Inventories;

/// <summary>
/// A single product tracked in stock. Belongs to a <see cref="Inventory"/> rather than directly to a
/// user: who may read or change this item is entirely decided by who may read or change its inventory
/// (see InventoryAccessResolver), so an item carries no owner and no access level of its own. Has no
/// edit-lock concept, unlike Note/TaskList/CalendarEvent - see Inventory's class comment.
/// </summary>
public sealed class InventoryItem
{
    public Guid Id { get; private set; }
    public Guid InventoryId { get; private set; }
    public string Name { get; private set; }
    public string ProductType { get; private set; }
    /// <summary>
    /// What this is filed under, as many words as apply - the same shape a task entry's categories have
    /// (see Orbit.Core.Tasks.TaskItem.Categories). It was a single word, which asked somebody stocking a
    /// shelf to decide whether the flour was "baking" or "dry goods" when it is plainly both.
    ///
    /// Tidied the way a task entry's are: trimmed, blanks dropped, and repeats folded case-insensitively,
    /// so "Food, food" is one category rather than two spellings of one.
    /// </summary>
    public IReadOnlyList<string> Categories { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal? MinimumQuantity { get; private set; }

    /// <summary>What <see cref="Quantity"/> and <see cref="MinimumQuantity"/> are counted in.</summary>
    public InventoryUnit Unit { get; private set; }

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

    /// <summary>
    /// Where this item sits on its inventory's shelf, as somebody arranged it. Kept because an inventory
    /// is read in an order that means something to whoever stocks it - what is next to what - which an
    /// alphabetical list does not preserve. Everything starts at zero and falls back to name order.
    /// </summary>
    public int Position { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Whether this item has dropped strictly below its own minimum - always false when no minimum is
    /// set. Sitting exactly at the minimum counts as fine: the minimum is the level to keep, not the
    /// level that already needs restocking, so 1 of 1 raises no task.
    /// </summary>
    public bool IsBelowMinimum => MinimumQuantity is { } minimumQuantity && Quantity < minimumQuantity;

    /// <summary>
    /// Something to look at every round rather than only when it runs low - milk, batteries, the things
    /// whose level nobody notices until they are gone.
    ///
    /// The restock list asks for these whatever the shelf says, so the answer comes from looking rather
    /// than from a count somebody forgot to keep up to date. That is the difference from
    /// <see cref="IsBelowMinimum"/>, which is a fact about the number stored here.
    /// </summary>
    public bool IsCheckedRegularly { get; private set; }

    /// <summary>
    /// Whether the restock list should be asking for this at all. Two reasons, either of which is
    /// enough: the shelf says it has run low, or somebody said this is one to look at every round.
    ///
    /// The second exists because the first only works for things whose count is kept up to date. Nobody
    /// counts the milk; they look. An item marked for checking is on the list whatever the number says,
    /// and crossing it off is the answer to "have you looked", not "is it above four".
    /// </summary>
    public bool BelongsOnTheRestockList => IsBelowMinimum || IsCheckedRegularly;

    private InventoryItem(
        Guid id, Guid inventoryId, string name, string productType, IReadOnlyList<string>? categories, decimal quantity,
        decimal? minimumQuantity,
        InventoryUnit unit, DateTimeOffset? expiryDate, NotificationChannel expiryNotificationChannel,
        Guid? pendingRestockTaskListId, Guid? pendingRestockTaskItemId, int position, DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        InventoryId = inventoryId;
        Name = name;
        ProductType = productType;
        Categories = Tidied(categories);
        Quantity = quantity;
        MinimumQuantity = minimumQuantity;
        Unit = unit;
        ExpiryDate = expiryDate;
        ExpiryNotificationChannel = expiryNotificationChannel;
        PendingRestockTaskListId = pendingRestockTaskListId;
        PendingRestockTaskItemId = pendingRestockTaskItemId;
        Position = position;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static InventoryItem Create(
        Guid inventoryId, string name, string productType, IReadOnlyList<string>? categories, decimal quantity,
        decimal? minimumQuantity,
        InventoryUnit unit, DateTimeOffset? expiryDate, NotificationChannel expiryNotificationChannel, int position = 0,
        bool isCheckedRegularly = false)
    {
        EnsureTheWordsFit(name, productType, categories);
        var now = DateTimeOffset.UtcNow;
        return new InventoryItem(
            Guid.NewGuid(), inventoryId, name, productType, categories, quantity, minimumQuantity, unit, expiryDate,
            expiryNotificationChannel, pendingRestockTaskListId: null, pendingRestockTaskItemId: null, position, now, now)
        {
            IsCheckedRegularly = isCheckedRegularly
        };
    }

    /// <summary>Rebuilds an inventory item from already-persisted values, bypassing creation rules.</summary>
    public static InventoryItem FromPersistence(
        Guid id, Guid inventoryId, string name, string productType, IReadOnlyList<string>? categories, decimal quantity,
        decimal? minimumQuantity,
        InventoryUnit unit, DateTimeOffset? expiryDate, NotificationChannel expiryNotificationChannel,
        Guid? pendingRestockTaskListId, Guid? pendingRestockTaskItemId, int position, DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc, bool isCheckedRegularly = false)
        => new(
            id, inventoryId, name, productType, categories, quantity, minimumQuantity, unit, expiryDate,
            expiryNotificationChannel, pendingRestockTaskListId, pendingRestockTaskItemId, position, createdAtUtc,
            updatedAtUtc)
        {
            IsCheckedRegularly = isCheckedRegularly
        };

    /// <summary>
    /// Brings this item up to the level it is meant to be kept at, which is what finishing its restock
    /// errand means: somebody went and got it. Answers whether anything changed - an item already at or
    /// above its minimum, or without one, is left exactly as it is rather than being pushed down to it.
    /// </summary>
    public bool TopUpToMinimum()
    {
        if (MinimumQuantity is not { } minimum || Quantity >= minimum)
        {
            return false;
        }

        Quantity = minimum;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return true;
    }

    /// <summary>Puts this item where the person arranging the shelf dropped it.</summary>
    public void MoveTo(int position)
    {
        Position = position;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Every field a caller may change, with nothing optional: a defaulted parameter here is how a
    /// caller that never mentioned the unit quietly resets it to pieces - the same trap
    /// TaskList.Update used to hold for priority.
    /// </summary>
    public void Update(
        string name, string productType, IReadOnlyList<string>? categories, decimal quantity, decimal? minimumQuantity,
        InventoryUnit unit, DateTimeOffset? expiryDate, NotificationChannel expiryNotificationChannel,
        bool isCheckedRegularly = false)
    {
        EnsureTheWordsFit(name, productType, categories);
        Name = name;
        IsCheckedRegularly = isCheckedRegularly;
        ProductType = productType;
        Categories = Tidied(categories);
        Quantity = quantity;
        MinimumQuantity = minimumQuantity;
        Unit = unit;
        ExpiryDate = expiryDate;
        ExpiryNotificationChannel = expiryNotificationChannel;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>What a shelf item may be called and classified as - see StoredTextLimits.</summary>
    private static void EnsureTheWordsFit(string name, string productType, IReadOnlyList<string>? categories)
    {
        StoredTextLimits.OrRefuse(name, StoredTextLimits.Title, "shelf item's name");
        StoredTextLimits.OrRefuse(productType, StoredTextLimits.ProductType, "shelf item's type");
        foreach (var category in categories ?? [])
        {
            StoredTextLimits.OrRefuse(category, StoredTextLimits.Category, "shelf item's category");
        }
    }

    /// <summary>
    /// The same tidying a task entry's categories get, and deliberately the same rule: one list of
    /// words in Orbit should not mean two different things depending on which screen typed it. See
    /// Orbit.Core.Tasks.CategoryText, which the forms use on the way in for the same reason.
    /// </summary>
    private static IReadOnlyList<string> Tidied(IReadOnlyList<string>? categories)
        => categories is null
            ? []
            : [.. categories
                .Select(category => category.Trim())
                .Where(category => category.Length > 0)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)];

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
