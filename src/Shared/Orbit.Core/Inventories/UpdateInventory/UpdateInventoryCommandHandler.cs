using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;

namespace Orbit.Core.Inventories.UpdateInventory;

public sealed class UpdateInventoryCommandHandler : IRequestHandler<UpdateInventoryCommand, EditOutcome>
{
    private readonly InventoryAccessResolver _inventoryAccessResolver;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly InventoryItemsSaver _itemsSaver;
    private readonly ShelfDemand _shelfDemand;
    private readonly ShelfUsage _shelfUsage;
    private readonly StockedEntryCompletion _stockedEntryCompletion;

    public UpdateInventoryCommandHandler(
        InventoryAccessResolver inventoryAccessResolver, IInventoryRepository inventoryRepository,
        IInventoryItemRepository inventoryItemRepository, ITaskRepository taskRepository,
        InventoryItemsSaver itemsSaver, ShelfDemand shelfDemand, ShelfUsage shelfUsage,
        StockedEntryCompletion stockedEntryCompletion)
    {
        _inventoryAccessResolver = inventoryAccessResolver;
        _inventoryRepository = inventoryRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _taskRepository = taskRepository;
        _itemsSaver = itemsSaver;
        _shelfDemand = shelfDemand;
        _shelfUsage = shelfUsage;
        _stockedEntryCompletion = stockedEntryCompletion;
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
        // After the amounts, and last: what the shelf covers depends on the count the step above may
        // just have moved - see InventoryItem.EffectiveMinimum.
        await CrossOffWhatTheShelfNowAnswersAsync(request, cancellationToken);
        return EditOutcome.Success;
    }

    /// <summary>
    /// Ticks off the errands this shelf has now answered. Asked for on 2026-09-18: saving an inventory
    /// should update and cross off the entries on the lists that point at it.
    ///
    /// The rule itself is not written here - <see cref="StockedEntryCompletion"/> holds it, and a save
    /// of a *list* has always gone through it. This is the same question asked from the other end, which
    /// is where it was missing: somebody stocking the shelf put four of something on it and the list
    /// standing in front of them went on asking for it until they next opened that list.
    ///
    /// Only the lists holding an outstanding entry for a row on this shelf, and only this reader's: a
    /// shelf reached through a share is measured against the lists of whoever is looking at it, which is
    /// the same reader ShelfUsage and ShelfDemand count for.
    /// </summary>
    private async Task CrossOffWhatTheShelfNowAnswersAsync(
        UpdateInventoryCommand request, CancellationToken cancellationToken)
    {
        var onThisShelf = (await _inventoryItemRepository.GetAllAsync(request.InventoryId, cancellationToken))
            .Select(shelfItem => shelfItem.Id)
            .ToHashSet();
        if (onThisShelf.Count == 0)
        {
            return;
        }

        var asking = (await _taskRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken))
            .Where(taskList => !taskList.IsPrivate && taskList.Items.Any(item =>
                !item.IsResolved && item.LinkedInventoryItemId is { } linked && onThisShelf.Contains(linked)))
            .ToList();
        if (asking.Count == 0)
        {
            return;
        }

        // Which entries were still outstanding, so the lists that actually moved can be told from the
        // ones that were only looked at. Taken before, because the crossing off happens where the
        // entries stand.
        var outstandingBefore = asking.ToDictionary(
            taskList => taskList.Id,
            taskList => taskList.Items.Where(item => !item.IsResolved).Select(item => item.Id).ToHashSet());

        // Asked once over every entry rather than once per list: it reads every shelf this reader has,
        // and paying for that per list would make a save cost more the more lists point at the shelf.
        await _stockedEntryCompletion.CrossOffWhatTheShelfCoversAsync(
            request.UserId, [.. asking.SelectMany(taskList => taskList.Items)], cancellationToken);

        var moved = asking
            .Where(taskList => taskList.Items.Any(item =>
                item.IsCompleted && outstandingBefore[taskList.Id].Contains(item.Id)))
            .ToList();
        if (moved.Count == 0)
        {
            return;
        }

        foreach (var taskList in moved)
        {
            // The entries were crossed off where they stand rather than handed in as a new set, so the
            // list is told to say again whether it is finished - and stamped, which is what carries the
            // tick to every other copy of it. See TaskList.RecountWhatIsDone.
            taskList.RecountWhatIsDone();
        }

        await _taskRepository.UpdateManyAsync(moved, cancellationToken);
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
