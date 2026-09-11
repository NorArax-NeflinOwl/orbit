using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Core.Inventories.CreateInventory;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks.StockCheck;

namespace Orbit.Core.Tasks.GenerateInventoryFromTaskList;

/// <summary>
/// Turns a list of work into the shelf that work needs: one entry per distinct thing it calls for, each
/// carrying how many the job needs as its minimum, and the list pointed at the result so the stock check
/// can be run straight away.
///
/// The minimum is counted the same way the check counts, by <see cref="StockRequirementCounter"/> -
/// repetition is quantity, so pasta named in three recipes has a minimum of three, and an entry that
/// says its own minimum adds that instead of one, so two recipes wanting two and three kilos of flour
/// ask the shelf to keep five. That is what makes a generated shelf a shopping list rather than a list
/// of headings, and asking the counter rather than adding up here is what keeps the shelf and the check
/// reading one number. What starts on the shelf is the smallest amount any of those entries wrote, and
/// where none wrote one, what the work has already ticked off: a line somebody has crossed out is a
/// thing they have, so three recipes with one done reads as one of three rather than none.
///
/// An entry that describes the thing it names (see <see cref="TaskItemProduct"/>) is taken at its word
/// for the rest: the unit, what it is filed under, how long it keeps and whether it is one to look at
/// every round are what somebody wrote on the entry. That is the point of letting an entry describe a
/// product before any shelf exists - the answer is given once, on the list, rather than typed again on
/// the storage afterwards.
///
/// Everything the tree names is included, including lines dated in the future - the shelf holds what the
/// whole job will need, while the check counts only what is due.
/// </summary>
public sealed class GenerateInventoryFromTaskListCommandHandler : IRequestHandler<GenerateInventoryFromTaskListCommand, Guid?>
{
    /// <summary>What a generated entry is filed under until somebody says otherwise.</summary>
    private const string GeneratedProductType = "Part";
    private const string GeneratedCategory = "From a task list";

    /// <summary>A checklist line says how many, never in what - so what it names is counted, one by one.</summary>
    private const InventoryUnit GeneratedUnit = InventoryUnit.Piece;

    /// <summary>What counts as the same thing here, matching StockRequirementCounter's own rule.</summary>
    private static readonly StringComparer SameName = StringComparer.CurrentCultureIgnoreCase;

    private readonly IDispatcher _dispatcher;
    private readonly ITaskRepository _taskRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IInventoryManagedTaskListRepository _managedTaskListRepository;
    private readonly RestockListRefresh _restockListRefresh;

    public GenerateInventoryFromTaskListCommandHandler(
        IDispatcher dispatcher, ITaskRepository taskRepository, IInventoryItemRepository inventoryItemRepository,
        IInventoryManagedTaskListRepository managedTaskListRepository, RestockListRefresh restockListRefresh)
    {
        _dispatcher = dispatcher;
        _taskRepository = taskRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _managedTaskListRepository = managedTaskListRepository;
        _restockListRefresh = restockListRefresh;
    }

    public async Task<Guid?> HandleAsync(GenerateInventoryFromTaskListCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskRepository.GetByIdAsync(request.UserId, request.TaskListId, cancellationToken);
        if (taskList is null || taskList.UserId != request.UserId)
        {
            return null;
        }

        var reachable = await _taskRepository.GetAllAsync(request.UserId, updatedSinceUtc: null, cancellationToken);
        var work = LinkedTaskListTree.WorkIn(taskList, reachable);
        var needed = StockRequirementCounter.CountRegardlessOfDueDate(work).Requirements;
        var described = ProductsDescribedIn(work);

        var name = string.IsNullOrWhiteSpace(request.Name) ? taskList.Title : request.Name.Trim();
        var inventoryId = await _dispatcher.SendAsync(
            new CreateInventoryCommand(request.UserId, name), cancellationToken);

        // Before a single row goes on the shelf: the settings decide whether there is a restock list at
        // all and what it asks for, and writing them afterwards would mean the first errands were raised
        // by rules nobody chose and then taken away again.
        if (request.RestockList is { } restockList)
        {
            await _managedTaskListRepository.SetSettingsAsync(inventoryId, restockList, cancellationToken);
        }

        // The rows go in one at a time rather than through UpdateInventoryCommand: that command writes
        // the inventory row as well, and an inventory created and updated inside one request leaves the
        // same key tracked twice.
        // In the order the work asks for things rather than alphabetically: a shelf built from a
        // shopping list reads best in the order the list reads - see InventoryItem.Position.
        var shelfItemIdsByName = new Dictionary<string, Guid>(SameName);
        // Which of the new rows already hold what they were asked to hold. Read off the rows being
        // built rather than by reading the shelf back, which at this point is a shelf this request has
        // only just written - see StockedEntryCompletion.Covers for the rule itself.
        var coveredShelfItemIds = new HashSet<Guid>();
        foreach (var (requirement, position) in needed.Select((requirement, position) => (requirement, position)))
        {
            var product = described.GetValueOrDefault(requirement.Name);
            var shelfItem = InventoryItem.Create(
                inventoryId, requirement.Name,
                Filled(product?.ProductType, GeneratedProductType),
                // As many words as apply, like every other shelf item - see InventoryItem.Categories.
                // An entry that named none is filed where a generated row has always been filed.
                product?.Categories is { Count: > 0 } categories ? categories : [GeneratedCategory],
                // Both amounts are heard from every entry naming the thing rather than the first one to
                // describe it - see StockRequirement.StartingStock and StockRequirementCounter.RequiredBy.
                requirement.StartingStock,
                requirement.Required,
                product?.Unit ?? GeneratedUnit,
                product?.ExpiryDate,
                product?.ExpiryNotificationChannel ?? NotificationChannel.None,
                position,
                product?.IsCheckedRegularly ?? false);
            await _inventoryItemRepository.AddAsync(shelfItem, cancellationToken);
            shelfItemIdsByName[requirement.Name] = shelfItem.Id;
            if (StockedEntryCompletion.Covers(shelfItem))
            {
                coveredShelfItemIds.Add(shelfItem.Id);
            }
        }

        // Each entry now stands for the row it asked for, rather than only sharing its wording. That link
        // is what every other screen reads an errand through - what it is about, where that is, and which
        // other list is asking for the same thing (see GetInventoryReferences) - and what a list set to
        // follow the plan counts (see RestockListRefresh). Only this list's own entries: the lists it
        // links to are their own, and pointing their entries at a shelf built somewhere else would be
        // deciding something about them from here.
        foreach (var entry in taskList.Items)
        {
            if (entry.Kind == TaskItemKind.Inventory
                && entry.LinkedInventoryItemId is null
                && shelfItemIdsByName.TryGetValue(entry.Description.Trim(), out var shelfItemId))
            {
                entry.PointAtShelfItem(shelfItemId);

                // An entry that said "four of these, and keep two" has been answered by the row it just
                // built: it is asking for nothing, so it is crossed off rather than left outstanding on
                // a list where every other line still means something. Only ever crossed off - see
                // StockedEntryCompletion, which is what does this on every later save.
                if (coveredShelfItemIds.Contains(shelfItemId))
                {
                    entry.Complete();
                }
            }
        }

        // The list's own "everything here is done" follows what the loop above crossed off; nothing else
        // here would say it again, and a generated list whose every line is covered should read as done.
        taskList.RecountWhatIsDone();
        taskList.LinkToInventory(inventoryId);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);

        // The standing "keep your stock updated" reminder exists from an inventory's first item, the same
        // as when items are added through the inventory editor - and so does an errand for anything the
        // shelf is already short of, which is most of a shelf built from work nobody has done yet. Built
        // by the refresh rather than by ensuring the list alone, so a storage generated with the restock
        // list switched off gets no list, and one narrowed to the round asks only about the round.
        if (needed.Count > 0)
        {
            await _restockListRefresh.RefreshAsync(inventoryId, cancellationToken);
        }

        return inventoryId;
    }

    /// <summary>
    /// What each named thing was described as - its type, unit, categories, expiry and whether it is
    /// looked at every round - by the first entry that described it. First rather than merged: those
    /// answers do not add up the way amounts do, and merging them would quietly make up a product neither
    /// entry describes. The amounts are not read from here at all; the counter hears every entry for
    /// those.
    /// </summary>
    private static Dictionary<string, TaskItemProduct> ProductsDescribedIn(IReadOnlyList<TaskItem> work)
    {
        var described = new Dictionary<string, TaskItemProduct>(SameName);
        foreach (var entry in work.Where(entry => entry.Product is not null))
        {
            described.TryAdd(entry.Description.Trim(), entry.Product!);
        }

        return described;
    }

    /// <summary>
    /// What somebody wrote, or what a line nobody filled in is filed under. A blank box is not an answer
    /// - it is the box being left alone - so it reads as the same "Part" every generated row has always
    /// been filed under rather than as an empty product type.
    /// </summary>
    private static string Filled(string? written, string fallback)
        => string.IsNullOrWhiteSpace(written) ? fallback : written.Trim();
}
