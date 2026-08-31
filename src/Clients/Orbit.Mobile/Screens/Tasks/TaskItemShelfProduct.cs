using Orbit.Contracts.Inventory;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Inventory;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// The product an Inventory entry is an errand about, opened for editing from the task list rather than
/// from the warehouse.
///
/// The point of giving these entries a kind and a link at all: the row already knows which product it
/// means, so correcting an amount should not mean leaving the list, finding the warehouse and finding
/// the product in it again. The same reason Orbit.Web puts these fields on its task editor.
///
/// The editor itself is the warehouse screen's, unchanged - one product form, wherever it is opened
/// from. What is added here is where the product lives, which is what the write-back needs and what the
/// reader is told before they change anything.
/// </summary>
/// <param name="WarehouseLocalId">This phone's id for the warehouse, which is what the write-back needs.</param>
/// <param name="WarehouseName">Said on screen, so nobody edits a shelf without being told which.</param>
public sealed record TaskItemShelfProduct(
    Guid WarehouseLocalId, string WarehouseName, WarehouseItemEditor Product)
{
    public static TaskItemShelfProduct For(
        Guid warehouseLocalId, string warehouseName, WarehouseItemDto product, Translations translations)
        // No name suggestions here, and Orbit.Web offers none on this form either: the box is a
        // correction to a product that already exists, not somewhere a new name is being invented.
        => new(warehouseLocalId, warehouseName, WarehouseItemEditor.For(product, translations));
}
