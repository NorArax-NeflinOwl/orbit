using CommunityToolkit.Mvvm.ComponentModel;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Contracts.Inventories;
using Orbit.Core.Tasks;
using Orbit.Core.Inventories;
using Orbit.Core.Notifications;
using Orbit.Core.Suggestions;
using Orbit.Mobile.Screens.Suggestions;

namespace Orbit.Mobile.Screens.Inventory;

/// <summary>
/// One inventory item while it is being edited. Everything Orbit.Web's inventory editor offers, which
/// until now the phone could neither see nor set: it created every item as one Piece of General with no
/// minimum and no expiry, and gave no way to change any of it.
///
/// A separate object from <see cref="InventoryItemRequest"/> because a form holds half-typed values - an
/// empty quantity box is not zero, and a date being picked is not yet a date - and the DTO's types
/// cannot express that.
/// </summary>
public sealed partial class InventoryItemEditor : ObservableObject
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

    /// <summary>
    /// What it is filed under, as many as apply, on one line and separated by commas - the same box
    /// this screen's sibling offers for a task entry, and the same rule behind it (see CategoryText).
    /// One box rather than the browser's chips: a phone form is a column of boxes, and a control that
    /// only exists here is one more thing to learn on the smaller screen.
    /// </summary>
    [ObservableProperty]
    private string _categories = string.Empty;

    [ObservableProperty]
    private string _quantity = "1";

    /// <summary>Empty means no minimum, which is not the same as a minimum of zero.</summary>
    [ObservableProperty]
    private string _minimumQuantity = string.Empty;

    /// <summary>
    /// How long this keeps, rather than the day it stops keeping - the question Orbit.Web's editor asks
    /// since its own rebuild, and the one somebody stocking a shelf can actually answer. A date is
    /// still what gets stored: the expiry reminder needs one (see ExpiryPeriod).
    /// </summary>
    public IReadOnlyList<ExpiryUnitChoice> ExpiryUnits { get; private init; } = [];

    [ObservableProperty]
    private ExpiryUnitChoice? _chosenExpiryUnit;

    /// <summary>How many of the chosen unit. Hidden, along with the date, while nothing expires.</summary>
    [ObservableProperty]
    private string _expiresIn = "1";

    /// <summary>True once a unit has been chosen, which is what the number and the date hang off.</summary>
    public bool Expires => ChosenExpiryUnit is { Unit: not ExpiryUnit.None };

    /// <summary>
    /// The day this lands on, said quietly beside the boxes. Somebody who set "2 weeks" is owed the
    /// answer to "when exactly", and so is somebody reading a shelf - the web says it in the same place.
    /// </summary>
    public string ExpiresOn
        => ExpiryDate is { } date ? date.ToLocalTime().ToString("d", _displayCulture) : string.Empty;

    public bool HasExpiryDate => ExpiryDate is not null;

    /// <summary>What <see cref="ToDto"/> stores, worked out from the two boxes above.</summary>
    private DateTimeOffset? ExpiryDate
        => new ExpiryPeriod(ParseExpiresIn(), ChosenExpiryUnit?.Unit ?? ExpiryUnit.None).On(DateTime.Today);

    private System.Globalization.CultureInfo _displayCulture = System.Globalization.CultureInfo.CurrentCulture;

    [ObservableProperty]
    private string _expiryNotificationChannel = nameof(NotificationChannel.Push);

    /// <summary>
    /// Whether the restock list asks for this every round, however much there is - see
    /// Orbit.Core.Inventories.InventoryItem.BelongsOnTheRestockList. A different question from the
    /// minimum above it: that one asks when there is too little, this one asks always.
    /// </summary>
    [ObservableProperty]
    private bool _isCheckedRegularly;

    private InventoryItemEditor(Guid? id) => _id = id;

    /// <summary>
    /// Names already on the shelves, offered while this one is typed - the field the suggestion feature
    /// exists for, since the same product gets typed twenty ways. Null when the editor was built without
    /// one, which is what every test that is not about suggestions does.
    /// </summary>
    public NameSuggestions? Suggestions { get; private init; }

    /// <summary>
    /// A product being put on a shelf by something else - a task entry describing it - which names it
    /// and so needs no name box. The two amounts are the ones generating an inventory from a list uses:
    /// one of the thing wanted, none of it there yet, counted in pieces.
    /// </summary>
    public static InventoryItemEditor ForSomethingNotOnTheShelfYet(Translations translations)
    {
        var editor = For(
            new InventoryItemRequest(
                null, string.Empty, string.Empty, string.Empty, Quantity: 0, MinimumQuantity: 1,
                // The unit's own name, not its short form: "pcs" is what a row is written with, and the
                // server parses this as an InventoryUnit - it would have refused the whole shelf.
                Unit: nameof(InventoryUnit.Piece), ExpiryDate: null, ExpiryNotificationChannel: "None"),
            translations);

        editor.ShowsName = false;
        return editor;
    }

    public static InventoryItemEditor For(
        InventoryItemRequest item, Translations translations, NameSuggestions? suggestions = null)
    {
        var editor = Build(item, translations, suggestions);
        // Set after the list exists rather than in the initialiser, which cannot pick out of a property
        // it is still assigning.
        editor.ChosenExpiryUnit = ExpiryUnitChoice.For(
            editor.ExpiryUnits, ExpiryPeriod.For(item.ExpiryDate, DateTime.Today).Unit);

        if (suggestions is not null)
        {
            suggestions.Offers(NameSuggestionKind.InventoryItemName);
            // Opened on a name rather than typed into: nothing is offered until the reader changes it,
            // or an item opened for its expiry date would be warned that it is a duplicate of itself.
            suggestions.StartsAt(editor.Name);
            suggestions.Takes = name => editor.Name = name;
        }

        return editor;
    }

    private static InventoryItemEditor Build(
        InventoryItemRequest item, Translations translations, NameSuggestions? suggestions)
        => new(item.Id)
        {
            Suggestions = suggestions,
            Channels = NotificationChannelChoice.All(translations),
            Units = InventoryUnitChoice.All(translations),
            Unit = item.Unit,
            Name = item.Name,
            ProductType = item.ProductType,
            Categories = CategoryText.Join(item.AllCategories),
            Quantity = item.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            MinimumQuantity = item.MinimumQuantity?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            ExpiryUnits = ExpiryUnitChoice.All(translations),
            ExpiresIn = ExpiryPeriod.For(item.ExpiryDate, DateTime.Today).Amount.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            _displayCulture = translations.DisplayCulture,
            ExpiryNotificationChannel = item.ExpiryNotificationChannel,
            // False for an item read from a server that says nothing about it, which is what an older
            // one does - see InventoryItemRequest.
            IsCheckedRegularly = item.IsCheckedRegularly ?? false
        };

    // A product named by the task entry describing it has nothing in its name box - there is no box -
    // and is named when the entry is saved. See ShowsName.
    public bool CanSave => (Name.Trim().Length > 0 || !ShowsName) && ParseQuantity() is not null;

    /// <summary>
    /// The item as the API takes it. The id travels through unchanged - a new one has none until the
    /// push comes back with it, and inventing one here would cut loose whatever pointed at the old.
    /// </summary>
    /// <summary>
    /// Whether this is a product being put on a shelf rather than one already there. The id is what
    /// says so: the server mints it, so a product that has one has been saved at least once.
    /// </summary>
    public bool IsSomethingNew => _id is null;

    /// <summary>
    /// Whether the form asks for the name at all. It does everywhere but on a product a task entry is
    /// describing, where the entry's own words are the name - see TaskItemShelfProduct.
    /// </summary>
    public bool ShowsName { get; private set; } = true;

    public InventoryItemRequest ToDto()
        => new(
            _id,
            Name.Trim(),
            // Left as the reader left them. Filling a blank box with "General" put a word nobody typed
            // into the filters above and onto the row, in English, whatever language the shelf was in.
            ProductType.Trim(),
            // The first of them in the old single field as well, so a server that has not learned
            // about several still reads one - see InventoryItemRequest.Category.
            CategoryText.Split(Categories).FirstOrDefault() ?? string.Empty,
            ParseQuantity() ?? 0,
            ParseMinimum(),
            Unit,
            // Converted rather than sent with the local offset the picker works in - see the same line
            // in TaskItemEditor for what a non-zero offset costs on the way to Postgres.
            ExpiryDate?.ToUniversalTime(),
            ExpiryNotificationChannel,
            // Always set on the way out, never left as null: null means "not provided" and would keep
            // whatever is stored, so a reader turning this off here would have been ignored.
            IsCheckedRegularly,
            CategoryText.Split(Categories));

    private int ParseExpiresIn()
        => int.TryParse(ExpiresIn.Trim(), out var amount) && amount > 0 ? amount : 1;

    partial void OnChosenExpiryUnitChanged(ExpiryUnitChoice? value)
    {
        OnPropertyChanged(nameof(Expires));
        SayWhenItLands();
    }

    partial void OnExpiresInChanged(string value) => SayWhenItLands();

    private void SayWhenItLands()
    {
        OnPropertyChanged(nameof(ExpiresOn));
        OnPropertyChanged(nameof(HasExpiryDate));
    }

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

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(CanSave));
        Suggestions?.ShowFor(value);
    }

    partial void OnQuantityChanged(string value) => OnPropertyChanged(nameof(CanSave));
}
