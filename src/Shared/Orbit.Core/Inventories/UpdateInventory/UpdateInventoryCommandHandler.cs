using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.UpdateInventory;

public sealed class UpdateInventoryCommandHandler : IRequestHandler<UpdateInventoryCommand, EditOutcome>
{
    private readonly InventoryAccessResolver _inventoryAccessResolver;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly InventoryItemsSaver _itemsSaver;
    private readonly ShelfDemand _shelfDemand;
    private readonly ShelfUsage _shelfUsage;

    public UpdateInventoryCommandHandler(
        InventoryAccessResolver inventoryAccessResolver, IInventoryRepository inventoryRepository,
        IInventoryItemRepository inventoryItemRepository, InventoryItemsSaver itemsSaver,
        ShelfDemand shelfDemand, ShelfUsage shelfUsage)
    {
        _inventoryAccessResolver = inventoryAccessResolver;
        _inventoryRepository = inventoryRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _itemsSaver = itemsSaver;
        _shelfDemand = shelfDemand;
        _shelfUsage = shelfUsage;
    }

    /// <summary>
    /// Mirrors UpdateTaskListCommandHandler: a read-only grantee gets NotFound, and someone else holding
    /// the edit lock gets Locked. The items themselves are written by <see cref="InventoryItemsSaver"/>,
    /// which creating an inventory now uses too.
    ///
    /// A save also carries the amounts back to the lists asking for them - see
    /// <see cref="WriteTheAmountsBackToTheListsAsync"/>, and ShelfDemand for the rule.
    /// </summary>
    public async Task<EditOutcome> HandleAsync(UpdateInventoryCommand request, CancellationToken cancellationToken)
    {
        var inventory = await _inventoryAccessResolver.ResolveAsync(request.UserId, request.InventoryId, cancellationToken);
        if (inventory is null)
        {
            return EditOutcome.NotFound;
        }

        // Visible but not theirs to change - see EditOutcomeKind.ReadOnly for why that is worth saying.
        if (!inventory.AccessLevel.AllowsEditing())
        {
            return EditOutcome.ReadOnly;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        if (inventory.IsLockedByAnotherUser(request.UserId, nowUtc))
        {
            return EditOutcome.LockedBy(inventory.LockedByUserName!);
        }

        // Said nothing about the description, keep the stored one - see UpdateInventoryCommand.
        inventory.Update(
            request.Name, request.IsPrivate, request.EncryptedContent,
            request.Description ?? inventory.Description);
        await _inventoryRepository.UpdateAsync(inventory, cancellationToken);

        if (inventory.IsPrivate)
        {
            await _itemsSaver.RemoveEverythingAsync(request.InventoryId, cancellationToken);
            return EditOutcome.Success;
        }

        // Read before the save, because the save is what overwrites them: which minimums this actually
        // moved is the whole question, and afterwards every row looks like the one that was typed.
        var minimumsBefore = (await _inventoryItemRepository.GetAllAsync(request.InventoryId, cancellationToken))
            .ToDictionary(item => item.Id, item => item.MinimumQuantity);

        await _itemsSaver.SaveAsync(request.InventoryId, request.Items, cancellationToken);
        await WriteTheAmountsBackToTheListsAsync(request, minimumsBefore, cancellationToken);
        return EditOutcome.Success;
    }

    /// <summary>
    /// Carries a minimum somebody just changed on the shelf back to the entries that ask for it, so
    /// editing an inventory updates the lists the way editing a list updates the inventory.
    ///
    /// Only the rows whose minimum actually moved: leaving every other row alone is what lets somebody
    /// correct a count, or add a row, without quietly rewriting amounts across their lists. Clearing a
    /// minimum is left alone too - "no minimum here" is not an amount to ask a list for, and the shelf
    /// goes on being kept at what the lists want (see InventoryItem.EffectiveMinimum).
    ///
    /// The count is taken again afterwards for exactly the rows that were written, because the lists now
    /// ask for something different and <see cref="InventoryItem.Usage"/> is the sum of what they ask.
    /// </summary>
    private async Task WriteTheAmountsBackToTheListsAsync(
        UpdateInventoryCommand request, IReadOnlyDictionary<Guid, decimal?> minimumsBefore,
        CancellationToken cancellationToken)
    {
        var moved = new Dictionary<Guid, decimal>();
        foreach (var item in request.Items)
        {
            if (item.Id is { } id
                && item.MinimumQuantity is { } minimum
                && minimumsBefore.TryGetValue(id, out var before)
                && before != minimum)
            {
                moved[id] = minimum;
            }
        }

        if (moved.Count == 0)
        {
            return;
        }

        var written = await _shelfDemand.WriteBackAsync(
            request.UserId, moved, request.SplitEvenlyAcross?.ToHashSet() ?? [], cancellationToken);
        if (written.Count > 0)
        {
            await _shelfUsage.RecountAsync(request.UserId, written, cancellationToken);
        }
    }
}
