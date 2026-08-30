using CommunityToolkit.Mvvm.ComponentModel;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Contracts.Inventory;
using Orbit.Core.Inventory;
using Orbit.Core.Notifications;

namespace Orbit.Mobile.Screens.Inventory;

/// <summary>
/// One warehouse item while it is being edited. Everything Orbit.Web's warehouse editor offers, which
/// until now the phone could neither see nor set: it created every item as one Piece of General with no
/// minimum and no expiry, and gave no way to change any of it.
///
/// A separate object from <see cref="WarehouseItemDto"/> because a form holds half-typed values - an
/// empty quantity box is not zero, and a date being picked is not yet a date - and the DTO's types
/// cannot express that.
/// </summary>
public sealed partial class WarehouseItemEditor : ObservableObject
{
    /// <summary>
    /// What the web's dropdown offers, in the same order and with the same wording - see
    /// NotificationChannelChoice. Taken in the factory rather than set afterwards: the picker reads it
    /// once, when the form appears, and a list handed over after that is never looked at.
    /// </summary>
    public IReadOnlyList<NotificationChannelChoice> Channels { get; private init; } = [];

    /// <summary>Bound to the picker, which needs a choice out of Channels rather than a string.</summary>
    public NotificationChannelChoice? ChosenExpiryNotificationChannel
    {
        get => NotificationChannelChoice.For(Channels, ExpiryNotificationChannel);
        set
        {
            if (value is not null)
            {
                ExpiryNotificationChannel = value.Value;
            }
        }
    }

    /// <summary>
    /// Every unit the picker offers - see <see cref="InventoryUnitChoice"/>. Taken in the factory for
    /// the same reason as <see cref="Channels"/>: the picker reads it once, when the form appears.
    /// </summary>
    public IReadOnlyList<InventoryUnitChoice> Units { get; private init; } = [];

    /// <summary>Bound to the picker, which needs a choice out of Units rather than a string.</summary>
    public InventoryUnitChoice? ChosenUnit
    {
        get => InventoryUnitChoice.For(Units, Unit);
        set
        {
            if (value is not null)
            {
                Unit = value.Value;
            }
        }
    }

    private readonly Guid? _id;

    /// <summary>
    /// What the two amounts are counted in. A fixed list rather than free text like the type and
    /// category above: the quantity and the minimum are compared as bare numbers, so both have to mean
    /// the same thing.
    /// </summary>
    [ObservableProperty]
    private string _unit = nameof(InventoryUnit.Piece);

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _productType = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _quantity = "1";

    /// <summary>Empty means no minimum, which is not the same as a minimum of zero.</summary>
    [ObservableProperty]
    private string _minimumQuantity = string.Empty;

    [ObservableProperty]
    private bool _expires;

    [ObservableProperty]
    private DateTime _expiryDate = DateTime.Today;

    [ObservableProperty]
    private string _expiryNotificationChannel = nameof(NotificationChannel.Push);

    private WarehouseItemEditor(Guid? id) => _id = id;

    public static WarehouseItemEditor For(WarehouseItemDto item, Translations translations)
        => new(item.Id)
        {
            Channels = NotificationChannelChoice.All(translations),
            Units = InventoryUnitChoice.All(translations),
            Unit = item.Unit,
            Name = item.Name,
            ProductType = item.ProductType,
            Category = item.Category,
            Quantity = item.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            MinimumQuantity = item.MinimumQuantity?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            Expires = item.ExpiryDate is not null,
            ExpiryDate = item.ExpiryDate?.LocalDateTime.Date ?? DateTime.Today,
            ExpiryNotificationChannel = item.ExpiryNotificationChannel
        };

    public bool CanSave => Name.Trim().Length > 0 && ParseQuantity() is not null;

    /// <summary>
    /// The item as the API takes it. The id travels through unchanged - a new one has none until the
    /// push comes back with it, and inventing one here would cut loose whatever pointed at the old.
    /// </summary>
    public WarehouseItemDto ToDto()
        => new(
            _id,
            Name.Trim(),
            // Left as the reader left them. Filling a blank box with "General" put a word nobody typed
            // into the filters above and onto the row, in English, whatever language the shelf was in.
            ProductType.Trim(),
            Category.Trim(),
            ParseQuantity() ?? 0,
            ParseMinimum(),
            Unit,
            // Converted rather than sent with the local offset the picker works in - see the same line
            // in TaskItemEditor for what a non-zero offset costs on the way to Postgres.
            Expires
                ? new DateTimeOffset(ExpiryDate.Date, TimeZoneInfo.Local.GetUtcOffset(ExpiryDate.Date)).ToUniversalTime()
                : null,
            ExpiryNotificationChannel);

    private decimal? ParseQuantity()
        => decimal.TryParse(Quantity.Trim(), System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var quantity) && quantity >= 0
            ? quantity
            : null;

    private decimal? ParseMinimum()
        => decimal.TryParse(MinimumQuantity.Trim(), System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var minimum) && minimum >= 0
            ? minimum
            : null;

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(CanSave));

    partial void OnQuantityChanged(string value) => OnPropertyChanged(nameof(CanSave));
}
