using Orbit.Contracts.Inventories;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Inventory;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// The product an Inventory entry is an errand about, opened for editing from the task list rather than
/// from the inventory.
///
/// The point of giving these entries a kind and a link at all: the row already knows which product it
/// means, so correcting an amount should not mean leaving the list, finding the inventory and finding
/// the product in it again. The same reason Orbit.Web puts these fields on its task editor.
///
/// The editor itself is the inventory screen's, unchanged - one product form, wherever it is opened
/// from. What is added here is where the product lives, which is what the write-back needs and what the
/// reader is told before they change anything.
/// </summary>
/// <param name="InventoryLocalId">This phone's id for the inventory, which is what the write-back needs.</param>
/// <param name="InventoryName">Said on screen, so nobody edits a shelf without being told which.</param>
public sealed record TaskItemShelfProduct(
    Guid InventoryLocalId, string InventoryName, InventoryItemEditor Product)
{
    public static TaskItemShelfProduct For(
        Guid inventoryLocalId, string inventoryName, InventoryItemRequest product, Translations translations)
        // No name suggestions here, and Orbit.Web offers none on this form either: the box is a
        // correction to a product that already exists, not somewhere a new name is being invented.
        => new(inventoryLocalId, inventoryName, InventoryItemEditor.For(product, translations));

    /// <summary>
    /// A product this shelf has not got yet, described by an entry on a list measured against it. No id:
    /// it is put there by the save, and it is named after the entry's own words - which is what the
    /// stock check matches the two by, so the form asks everything except the name.
    ///
    /// The defaults are the ones generating a storage from a list already uses, and the same two
    /// Orbit.Web's form starts on: one of the thing wanted, none of it there yet, counted in pieces.
    /// Two ways onto one shelf should not disagree about what an entry asking for something means.
    /// </summary>
    public static TaskItemShelfProduct ForSomethingNotOnTheShelfYet(
        Guid inventoryLocalId, string inventoryName, Translations translations)
        => new(
            inventoryLocalId,
            inventoryName,
            InventoryItemEditor.ForSomethingNotOnTheShelfYet(translations));
}
