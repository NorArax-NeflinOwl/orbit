using Orbit.Contracts.Inventory;

namespace Orbit.Web.Services;

/// <summary>
/// One shelf item as a form holds it. Shared by the warehouse editor and by a task list's inventory
/// errand, which edits the same product from the other side - see InventoryFields.razor. Two models for
/// one thing is how the two forms came to offer different fields.
/// </summary>
public sealed class InventoryItemFormModel
{
    /// <summary>Null for a row added in this session and not yet saved.</summary>
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal? MinimumQuantity { get; set; }

    /// <summary>What the two amounts are counted in - see InventoryUnitOption.</summary>
    public string Unit { get; set; } = InventoryUnitOption.Default.Value;

    /// <summary>
    /// The day it stops keeping. Kept as the wire's own type so neither form has to convert: the field
    /// asks how long it keeps rather than for a date, and turning "a fortnight" into the 14th happens
    /// in one place - see ExpiresInField.
    /// </summary>
    public DateTimeOffset? ExpiryDate { get; set; }

    public string ExpiryNotificationChannel { get; set; } = "Push";

    /// <summary>Asked for every round, whatever the count says - see InventoryItem.BelongsOnTheRestockList.</summary>
    public bool IsCheckedRegularly { get; set; }

    public static InventoryItemFormModel FromDto(InventoryItemDto item)
        => new()
        {
            Id = item.Id,
            Name = item.Name,
            ProductType = item.ProductType,
            Category = item.Category,
            Quantity = item.Quantity,
            MinimumQuantity = item.MinimumQuantity,
            // Read through the option list rather than taken as it comes: a private warehouse sealed
            // before units existed has none, and the picker would then show pieces while the row held
            // nothing - see InventoryUnitOption.For.
            Unit = InventoryUnitOption.For(item.Unit).Value,
            ExpiryDate = item.ExpiryDate,
            ExpiryNotificationChannel = item.ExpiryNotificationChannel,
            IsCheckedRegularly = item.IsCheckedRegularly
        };

    /// <summary>
    /// The row as a save sends it. Npgsql only accepts a DateTimeOffset with a zero offset for a
    /// "timestamp with time zone" column, so the chosen local day is converted to the instant it began
    /// rather than merely re-labelled.
    /// </summary>
    public WarehouseItemDto ToDto()
        => new(
            Id, Name, ProductType, Category, Quantity, MinimumQuantity, Unit,
            ExpiryDate is { } expiresOn ? expiresOn.ToUniversalTime() : null,
            ExpiryNotificationChannel, IsCheckedRegularly);

    /// <summary>Whether the shelf says there is less of this than somebody asked to keep.</summary>
    public bool IsBelowMinimum => MinimumQuantity is { } minimum && Quantity < minimum;
}
