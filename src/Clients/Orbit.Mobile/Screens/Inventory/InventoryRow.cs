using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Inventory;

/// <summary>One row of the inventories screen - the same shape as the other three features' rows.</summary>
/// <param name="Contents">Already in the reader's language, so the row itself needs no dictionary.</param>
/// <param name="IsHidden"><inheritdoc cref="Notes.NoteListItem.IsHidden" path="/summary"/></param>
/// <param name="IsSharedWithMe">
/// <inheritdoc cref="Tasks.TaskListRow" path="/param[@name='IsSharedWithMe']/node()"/>
/// </param>
/// <param name="CanBeShared">
/// Whether the card may offer to hand this on. Three things have to hold: a private inventory is
/// offered to nobody, since the server keeps no readable copy to hand over; one the server has not
/// seen yet has no id to share; and a share that arrived read-only grants nothing further. Orbit.Web
/// asks the same three and leaves the entry out rather than drawing it spent.
/// </param>
public sealed record InventoryRow(
    Guid LocalId, string Name, int ItemCount, bool HasUnsentChanges, OfflineEditRefusal Refusal,
    string Contents, string Status, bool IsHidden = false, string HiddenName = "Private",
    bool IsCopy = false, bool IsSharedWithMe = false, bool CanBeShared = false,
    string RunningLow = "")
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
            IsCopy: inventory.CopyOfLocalId is not null, IsSharedWithMe: inventory.IsShared,
            CanBeShared: inventory is { ServerId: not null, IsPrivate: false }
                && SharedItemAccess.AllowsSharing(inventory),
            RunningLow: RunningLowOn(inventory, translations));
    }

    /// <summary>
    /// How many things on this shelf are below the minimum somebody set for them, ready to be read -
    /// or nothing at all when none are, because a shelf that is stocked has nothing to say about it.
    ///
    /// The same test the shelf's own rows make (see <see cref="InventoryItemRow"/>) and the same test
    /// Orbit.Web's editor makes: a minimum that is set and not met. It is worth saying on the list
    /// rather than only inside, because the reason for opening an inventory is usually to find out
    /// whether anything on it has run out - and until now that took opening every one of them.
    /// </summary>
    private static string RunningLowOn(LocalInventory inventory, Translations translations)
    {
        var low = inventory.Items.Count(
            item => item.MinimumQuantity is { } minimum && item.Quantity < minimum);

        return low == 0 ? string.Empty : translations.Format("{0} low", low);
    }

    /// <inheritdoc cref="Notes.NoteListItem.DisplayTitle"/>
    public string DisplayName => IsHidden ? HiddenName : Name;

    /// <inheritdoc cref="Notes.NoteListItem.CanBeOpened"/>
    public bool CanBeOpened => !IsHidden;

    /// <summary>
    /// <inheritdoc cref="Notes.NoteListItem.HasCardMenu" path="/summary/node()"/> Here it can be
    /// neither: an inventory shared read-only can be neither deleted nor handed on.
    /// </summary>
    public bool HasCardMenu => CanBeShared || !IsSharedWithMe;

    public bool HasStatus => Status.Length > 0 && !IsHidden;

    /// <summary>
    /// Whether the chip saying how much has run out belongs on this row. Nothing on a locked one: what
    /// a private inventory holds is exactly what being private keeps back, and a count is part of it.
    /// </summary>
    public bool HasRunningLow => RunningLow.Length > 0 && !IsHidden;
}
