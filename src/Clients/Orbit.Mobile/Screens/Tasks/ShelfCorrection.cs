using Orbit.Contracts.Inventories;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>What became of a correction to a shelf - what the screen has to say about it afterwards.</summary>
public enum ShelfCorrectionOutcome
{
    Applied,

    /// <summary>The inventory is not on this phone, so there was nothing to correct.</summary>
    NotFound,

    /// <summary>
    /// The inventory cannot be written to: somebody else can change it and there is no connection to
    /// check with, or it was shared without editing - see LocalWriteOutcome.
    /// </summary>
    Refused
}

/// <summary>
/// A product corrected from an Inventory errand, written back to the shelf it lives on.
///
/// Its own object rather than three more fields on the task list screen: the inventory store, its
/// synchroniser and the inventory client only ever travel together and only ever serve this one job.
/// </summary>
public sealed class ShelfCorrection
{
    private readonly LocalInventoryRepository _inventories;
    private readonly InventorySynchronizer _synchronizer;
    private readonly InventoryClient _inventoryClient;

    public ShelfCorrection(
        LocalInventoryRepository inventories, InventorySynchronizer synchronizer, InventoryClient inventoryClient)
    {
        _inventories = inventories;
        _synchronizer = synchronizer;
        _inventoryClient = inventoryClient;
    }

    /// <summary>
    /// Every shelf this phone holds - what lets an errand say which shelf it is about, and which other
    /// list is asking for the same product. Read locally, so it is there with no connection.
    /// </summary>
    public Task<IReadOnlyList<LocalInventory>> ShelvesAsync(CancellationToken cancellationToken)
        => _inventories.GetAllAsync(cancellationToken);

    /// <summary>
    /// Writes the corrected product back, then asks that inventory to work out its restock list again -
    /// a corrected amount can settle an errand or raise one, and a list still saying the old thing makes
    /// the correction look like it did not take.
    ///
    /// Called after the task list is saved rather than before, and it does not stop the save if it
    /// fails: the shelf is a second thing that screen touches, not the thing it is for. That is the
    /// order Orbit.Web settles on too, and the opposite of the calendar's - an appointment has to exist
    /// before the entry can name it, while a product already exists and is only being corrected.
    /// </summary>
    public async Task<ShelfCorrectionOutcome> ApplyAsync(
        TaskItemShelfProduct shelf, CancellationToken cancellationToken)
    {
        if (await _inventories.FindAsync(shelf.InventoryLocalId, cancellationToken) is not { } inventory)
        {
            return ShelfCorrectionOutcome.NotFound;
        }

        var corrected = shelf.Product.ToDto();
        var outcome = await _inventories.UpdateAsync(
            shelf.InventoryLocalId,
            new InventoryContent(inventory.Name, ShelfWith(inventory, corrected), inventory.IsPrivate),
            cancellationToken);

        if (outcome.WasRefused())
        {
            return ShelfCorrectionOutcome.Refused;
        }

        // Pushed here rather than left for whenever somebody next opens the inventory: the correction is
        // to a shelf the task list screen is not otherwise about, so nothing else would carry it up, and
        // a restock list rebuilt before the new amount arrives would be rebuilt from the old one.
        await _synchronizer.SynchroniseAsync(cancellationToken);
        await RebuildTheRestockListAsync(inventory.ServerId, cancellationToken);
        return ShelfCorrectionOutcome.Applied;
    }

    /// <summary>
    /// The shelf with this product on it: the one it corrects replaced, or the product added where it
    /// has no id yet.
    ///
    /// A shelf already holding something by that name is what the entry was asking for, so nothing is
    /// added for it - the stock check matches an errand to a product by name, and two rows of one name
    /// would be two answers to "is there enough". The same rule Orbit.Web's own save applies.
    /// </summary>
    private static IReadOnlyList<InventoryItemRequest> ShelfWith(LocalInventory inventory, InventoryItemRequest product)
    {
        if (product.Id is { } productId)
        {
            return [.. inventory.Items.Select(stored => stored.Id == productId ? product : stored)];
        }

        var alreadyThere = inventory.Items.Any(stored =>
            string.Equals(stored.Name.Trim(), product.Name.Trim(), StringComparison.CurrentCultureIgnoreCase));

        return alreadyThere ? inventory.Items : [.. inventory.Items, product];
    }

    /// <summary>
    /// Best effort, and deliberately quiet: the correction is already saved on this phone and on its way
    /// up, and a restock list that is one sync behind rights itself. Saying "couldn't reach Orbit" about
    /// a change that did land would be the wrong thing to tell somebody.
    /// </summary>
    private async Task RebuildTheRestockListAsync(Guid? inventoryServerId, CancellationToken cancellationToken)
    {
        if (inventoryServerId is not { } serverId)
        {
            return;
        }

        try
        {
            await _inventoryClient.RefreshRestockListAsync(serverId, cancellationToken);
        }
        catch (HttpRequestException)
        {
        }
    }
}
