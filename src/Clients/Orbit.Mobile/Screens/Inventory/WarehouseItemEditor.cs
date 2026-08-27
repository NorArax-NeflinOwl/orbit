using CommunityToolkit.Mvvm.ComponentModel;
using Orbit.Contracts.Inventory;
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
    /// <summary>What the web's dropdown offers, in the same order - see NotificationChannel.</summary>
    public static IReadOnlyList<string> Channels { get; } =
        [.. Enum.GetValues<NotificationChannel>().Select(channel => channel.ToString())];

    private readonly Guid? _id;

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

    public static WarehouseItemEditor For(WarehouseItemDto item)
        => new(item.Id)
        {
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
            ProductType.Trim() is { Length: > 0 } type ? type : "General",
            Category.Trim() is { Length: > 0 } category ? category : "General",
            ParseQuantity() ?? 0,
            ParseMinimum(),
            Expires ? new DateTimeOffset(ExpiryDate.Date, TimeZoneInfo.Local.GetUtcOffset(ExpiryDate.Date)) : null,
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
