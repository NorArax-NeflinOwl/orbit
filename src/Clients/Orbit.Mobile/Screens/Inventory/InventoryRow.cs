using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Inventory;

/// <summary>One row of the inventories screen - the same shape as the other three features' rows.</summary>
/// <param name="Contents">Already in the reader's language, so the row itself needs no dictionary.</param>
/// <param name="IsHidden"><inheritdoc cref="Notes.NoteListItem.IsHidden" path="/summary"/></param>
public sealed record InventoryRow(
    Guid LocalId, string Name, int ItemCount, bool HasUnsentChanges, OfflineEditRefusal Refusal,
    string Contents, string Status, bool IsHidden = false, string HiddenName = "Private",
    bool IsCopy = false)
{
    public static InventoryRow From(
        LocalInventory inventory, bool hasUnsentChanges, INetworkStatus networkStatus, Translations translations,
        bool privateItemsAreUnlocked = true, string hiddenName = "Private")
    {
        var refusal = OfflineEditPolicy.Evaluate(inventory, networkStatus);

        return new(
            inventory.LocalId, inventory.IsSealed ? hiddenName : inventory.Name, inventory.Items.Count,
            hasUnsentChanges, refusal,
            translations.Format("Items: {0}", inventory.Items.Count),
            OfflineEditExplanation.For(inventory, refusal, hasUnsentChanges, translations),
            IsHidden: inventory.IsPrivate && !privateItemsAreUnlocked, HiddenName: hiddenName,
            IsCopy: inventory.CopyOfLocalId is not null);
    }

    /// <inheritdoc cref="Notes.NoteListItem.DisplayTitle"/>
    public string DisplayName => IsHidden ? HiddenName : Name;

    /// <inheritdoc cref="Notes.NoteListItem.CanBeOpened"/>
    public bool CanBeOpened => !IsHidden;

    public bool HasStatus => Status.Length > 0 && !IsHidden;
}
