using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Inventory;

/// <summary>One row of the warehouses screen - the same shape as the other three features' rows.</summary>
/// <param name="Contents">Already in the reader's language, so the row itself needs no dictionary.</param>
/// <param name="IsHidden"><inheritdoc cref="Notes.NoteListItem.IsHidden" path="/summary"/></param>
public sealed record WarehouseRow(
    Guid LocalId, string Name, int ItemCount, bool HasUnsentChanges, OfflineEditRefusal Refusal,
    string Contents, string Status, bool IsHidden = false, string HiddenName = "Private")
{
    public static WarehouseRow From(
        LocalWarehouse warehouse, bool hasUnsentChanges, INetworkStatus networkStatus, Translations translations,
        bool privateItemsAreUnlocked = true, string hiddenName = "Private")
    {
        var refusal = OfflineEditPolicy.Evaluate(warehouse, networkStatus);

        return new(
            warehouse.LocalId, warehouse.IsSealed ? hiddenName : warehouse.Name, warehouse.Items.Count,
            hasUnsentChanges, refusal,
            translations.Format("Items: {0}", warehouse.Items.Count),
            OfflineEditExplanation.For(refusal, hasUnsentChanges, translations),
            IsHidden: warehouse.IsPrivate && !privateItemsAreUnlocked, HiddenName: hiddenName);
    }

    /// <inheritdoc cref="Notes.NoteListItem.DisplayTitle"/>
    public string DisplayName => IsHidden ? HiddenName : Name;

    /// <inheritdoc cref="Notes.NoteListItem.CanBeOpened"/>
    public bool CanBeOpened => !IsHidden;

    public bool HasStatus => Status.Length > 0 && !IsHidden;
}
