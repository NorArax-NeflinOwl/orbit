using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.StockCheck;

namespace Orbit.Core.Inventories;

/// <summary>
/// Puts the product entries of a list onto the shelf that list is measured against.
///
/// An entry of <see cref="TaskItemKind.Inventory"/> names something the work needs. A shelf used to be
/// built from those entries only by "Generate inventory", which also points each entry at the row it
/// made; an entry added to the list afterwards stayed a line of text with a product description nothing
/// read. The browser and the phone each wrote such a product onto the shelf themselves, but neither
/// pointed the entry at the row it became - so no restock errand was ever about it, nothing crossed it
/// off when the shelf filled, and a client that did neither left it off the shelf altogether. It is done
/// here instead, where every save of a list passes, so it is one rule whoever saves.
///
/// Each entry either becomes a new row, named after its own words and holding what it describes, or is
/// matched to the row already holding something by that name - the name rule everything else matches an
/// entry to a product by. Either way it then stands for that row exactly as a generated entry does, and
/// its own description is dropped: the row is the answer from here on (see TaskItemProduct).
///
/// A row that already exists is pointed at and left as it is. Its minimum is not raised by the entry's:
/// a shopping list is reused, and a pantry's minimum that grew by one every week somebody wrote "milk"
/// again would be asking for more than anybody keeps.
///
/// A list measured against no shelf has nowhere to put anything and is left alone: its entries keep
/// describing what they want until a shelf is generated from them.
/// </summary>
public sealed class ProductEntryPlacement
{
    /// <summary>What counts as the same row when matching a name to a shelf, as everywhere else does.</summary>
    private static readonly StringComparer SameName = StringComparer.CurrentCultureIgnoreCase;

    private readonly InventoryAccessResolver _inventoryAccessResolver;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly RestockListRefresh _restockListRefresh;

    public ProductEntryPlacement(
        InventoryAccessResolver inventoryAccessResolver, IInventoryItemRepository inventoryItemRepository,
        RestockListRefresh restockListRefresh)
    {
        _inventoryAccessResolver = inventoryAccessResolver;
        _inventoryItemRepository = inventoryItemRepository;
        _restockListRefresh = restockListRefresh;
    }

    /// <summary>
    /// Points every product entry in <paramref name="incoming"/> that stands for nothing yet at a row on
    /// the shelf <paramref name="stored"/> is measured against, adding the rows that are missing. The
    /// entries are changed where they are, so the caller writes them with the save it was already making.
    ///
    /// Answers the inventory the entries were placed on, for the caller to settle its restock list once
    /// the list itself is written (see <see cref="SettleTheRestockListAsync"/>), or null when nothing was
    /// placed. Reads nothing for a list with no shelf or no waiting entry, which is nearly every save.
    ///
    /// Placed with the rights of whoever is saving rather than the list owner's: editing a list is not
    /// editing the storage it is measured against, so somebody who may only read that storage - or
    /// cannot see it at all - puts nothing on it. See <see cref="MayPutThingsOnAsync"/>.
    /// </summary>
    public async Task<Guid?> PlaceAsync(
        Guid callerUserId, TaskList stored, IReadOnlyList<TaskItem> incoming, bool isPrivate,
        CancellationToken cancellationToken)
    {
        if (stored.LinkedInventoryId is not { } inventoryId || isPrivate)
        {
            return null;
        }

        var waiting = StillWaiting(incoming, stored);
        if (waiting.Count == 0)
        {
            return null;
        }

        if (!await MayPutThingsOnAsync(callerUserId, inventoryId, cancellationToken))
        {
            return null;
        }

        var shelf = await _inventoryItemRepository.GetAllAsync(inventoryId, cancellationToken);
        var rowsByName = shelf
            .GroupBy(row => row.Name.Trim(), SameName)
            .ToDictionary(byName => byName.Key, byName => byName.ToList(), SameName);
        // After everything already there, in the order the list names them - see InventoryItem.Position.
        var nextPosition = shelf.Count == 0 ? 0 : shelf.Max(row => row.Position) + 1;

        var placedAny = false;
        // Grouped by the counter's own key, so each group is exactly one of its requirements.
        foreach (var sameThing in waiting.GroupBy(entry => entry.Description.Trim().ToLowerInvariant()))
        {
            var entries = sameThing.ToList();
            var row = rowsByName.TryGetValue(entries[0].Description.Trim(), out var named)
                ? OnlyOneOf(named)
                : await AddRowAsync(inventoryId, entries, nextPosition++, cancellationToken);
            if (row is null)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                entry.PointAtShelfItem(row.Id);
            }

            placedAny = true;
        }

        return placedAny ? inventoryId : null;
    }

    /// <summary>
    /// Brings the storage's restock list up to date with the rows placed on it: a new row is usually
    /// below its minimum, and the errand for it belongs on the list when it is next read rather than
    /// after somebody next saves the storage. Called once the task list is written, since the refresh
    /// reads every list and should read this one as saved.
    /// </summary>
    public Task SettleTheRestockListAsync(Guid inventoryId, CancellationToken cancellationToken)
        => _restockListRefresh.RefreshAsync(inventoryId, cancellationToken);

    /// <summary>
    /// The product entries that stand for no row yet. An entry whose stored copy already stands for one
    /// is pointed back at it rather than placed again: that is a client sending the entry as it was
    /// before its last save was placed - a browser tab saved twice, a phone that pushed before it pulled
    /// - and placing it a second time would read its product as something new.
    /// </summary>
    private static List<TaskItem> StillWaiting(IReadOnlyList<TaskItem> incoming, TaskList stored)
    {
        var standingFor = stored.Items
            .Where(item => item.LinkedInventoryItemId is not null)
            .ToDictionary(item => item.Id, item => item.LinkedInventoryItemId!.Value);

        var waiting = new List<TaskItem>();
        foreach (var entry in incoming.Where(IsWaitingForAShelf))
        {
            if (standingFor.TryGetValue(entry.Id, out var shelfItemId))
            {
                entry.PointAtShelfItem(shelfItemId);
                continue;
            }

            waiting.Add(entry);
        }

        return waiting;
    }

    /// <summary>
    /// Whether this caller may add rows to that storage now: the questions saving the storage itself
    /// asks (see UpdateInventoryCommandHandler) - that they can see it, may change it, and nobody else is
    /// editing it - and whether it keeps readable rows at all. Declined rather than written anyway: a
    /// storage somebody else is editing is saved whole from their screen, and a row put there meanwhile
    /// would be deleted by that save, leaving the entry pointing at nothing; a private one keeps no rows
    /// the server can read (see InventoryItemsSaver.RemoveEverythingAsync). The entries are then left
    /// describing what they want, and the next save tries again.
    /// </summary>
    private async Task<bool> MayPutThingsOnAsync(
        Guid callerUserId, Guid inventoryId, CancellationToken cancellationToken)
        => await _inventoryAccessResolver.ResolveAsync(callerUserId, inventoryId, cancellationToken) is { } inventory
            && inventory.AccessLevel.AllowsEditing()
            && !inventory.IsLockedByAnotherUser(callerUserId, DateTimeOffset.UtcNow)
            && !inventory.IsPrivate;

    private static bool IsWaitingForAShelf(TaskItem entry)
        => entry.Kind == TaskItemKind.Inventory
            && entry.LinkedInventoryItemId is null
            && !entry.IsALinkToOtherLists
            && entry.Description.Trim().Length > 0;

    /// <summary>
    /// The row an entry is about, when the shelf holds exactly one by its name. Two rows sharing a name
    /// give no answer to "which one", and guessing would point the errand at the wrong batch - so the
    /// entry is left describing what it wants, the same line InventoryTaskListCoordinator draws for a
    /// shortfall.
    /// </summary>
    private static InventoryItem? OnlyOneOf(IReadOnlyList<InventoryItem> named)
        => named.Count == 1 ? named[0] : null;

    /// <summary>
    /// A new row for what <paramref name="entries"/> ask for. The amounts are counted across all of them
    /// by <see cref="StockRequirementCounter"/>, the same count generating a storage makes - every
    /// minimum added up, the least amount written as what is there. The rest is the first description's,
    /// for the reason GenerateInventoryFromTaskListCommandHandler.ProductsDescribedIn gives, and a blank
    /// stays blank: this is what the browser and the phone wrote for such an entry before, and neither
    /// filled a box nobody typed in.
    /// </summary>
    private async Task<InventoryItem> AddRowAsync(
        Guid inventoryId, IReadOnlyList<TaskItem> entries, int position, CancellationToken cancellationToken)
    {
        var requirement = StockRequirementCounter.CountRegardlessOfDueDate(entries).Requirements.Single();
        var described = entries.FirstOrDefault(entry => entry.Product is not null) ?? entries[0];
        var product = described.Product;

        var row = InventoryItem.Create(
            inventoryId, requirement.Name,
            product?.ProductType.Trim() ?? string.Empty,
            // What the product is filed under, and failing that what its entry is: the browser's form has
            // one categories box for both, and an entry's answer is its product's too.
            product?.Categories is { Count: > 0 } categories ? categories : described.Categories,
            requirement.StartingStock,
            requirement.Required,
            product?.Unit ?? InventoryUnit.Piece,
            product?.ExpiryDate,
            product?.ExpiryNotificationChannel ?? NotificationChannel.None,
            position,
            product?.IsCheckedRegularly ?? false);
        await _inventoryItemRepository.AddAsync(row, cancellationToken);
        return row;
    }
}
