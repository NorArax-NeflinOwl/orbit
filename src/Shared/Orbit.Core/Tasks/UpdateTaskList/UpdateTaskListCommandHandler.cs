using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;

namespace Orbit.Core.Tasks.UpdateTaskList;

public sealed class UpdateTaskListCommandHandler : IRequestHandler<UpdateTaskListCommand, EditOutcome>
{
    private readonly TaskListAccessResolver _taskListAccessResolver;
    private readonly ITaskRepository _taskRepository;
    private readonly TaskListLinkValidator _taskListLinkValidator;
    private readonly RestockCompletion _restockCompletion;
    private readonly StockedEntryCompletion _stockedEntryCompletion;
    private readonly ProductEntryPlacement _productEntryPlacement;

    public UpdateTaskListCommandHandler(
        TaskListAccessResolver taskListAccessResolver, ITaskRepository taskRepository,
        TaskListLinkValidator taskListLinkValidator, RestockCompletion restockCompletion,
        StockedEntryCompletion stockedEntryCompletion, ProductEntryPlacement productEntryPlacement)
    {
        _taskListAccessResolver = taskListAccessResolver;
        _taskRepository = taskRepository;
        _taskListLinkValidator = taskListLinkValidator;
        _restockCompletion = restockCompletion;
        _stockedEntryCompletion = stockedEntryCompletion;
        _productEntryPlacement = productEntryPlacement;
    }

    /// <summary>Mirrors Orbit.Core.Notes.UpdateNote.UpdateNoteCommandHandler - see its class comment for what NotFound/Locked mean here.</summary>
    public async Task<EditOutcome> HandleAsync(UpdateTaskListCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskListAccessResolver.ResolveAsync(request.UserId, request.Id, cancellationToken);
        if (taskList is null)
        {
            return EditOutcome.NotFound;
        }

        // Visible but not theirs to change - see EditOutcomeKind.ReadOnly for why that is worth saying.
        if (!taskList.AccessLevel.AllowsEditing())
        {
            return EditOutcome.ReadOnly;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        if (taskList.IsLockedByAnotherUser(request.UserId, nowUtc))
        {
            return EditOutcome.LockedBy(taskList.LockedByUserName!);
        }

        // Scoped to the list's actual owner, not the caller (who may be editing via a share) - a linked
        // item only makes sense pointing at one of the *owner's* other task lists, the same universe
        // TaskListLinkValidator has always validated against.
        await _taskListLinkValidator.ValidateAsync(taskList.UserId, request.Id, request.Items, cancellationToken);

        // Clients name their own entries now, so two of them can hand over the same id. Both sides are
        // renamed when they do - see TaskItemIdentity for why neither may keep it.
        var identity = TaskItemIdentity.Resolve(
            request.Items,
            await _taskRepository.GetHoldingItemsAsync(
                taskList.UserId, request.Id, [.. request.Items.Select(item => item.Id)], cancellationToken));

        KeepTheCategoriesOfEntriesThatSaidNothing(identity.Items, taskList, request.EntriesKeepingTheirCategories);
        KeepWhatEntriesThatSaidNothingAlreadyAskFor(identity.Items, taskList, request.EntriesKeepingTheirProduct);
        KeepTheDescriptionOfEntriesThatSaidNothing(identity.Items, taskList, request.EntriesKeepingTheirNotes);
        KeepTheStepsOfEntriesThatSaidNothing(identity.Items, taskList, request.EntriesKeepingTheirSteps);
        KeepTheLookOfEntriesThatSaidNothing(identity.Items, taskList, request.EntriesKeepingTheirLook);
        KeepTheAlternativesOfEntriesThatSaidNothing(identity.Items, taskList, request.EntriesKeepingTheirAlternatives);

        // A product entry on a list measured against a shelf goes onto that shelf and stands for its row
        // from this save on - see ProductEntryPlacement. After the product has been kept for entries
        // that said nothing about it, so a client with no product form still has it placed as described,
        // and before the crossing-off below, so a row that already holds what its entry asked for
        // crosses the entry off in this same save, the way a generated one does.
        var placedOnInventoryId = await _productEntryPlacement.PlaceAsync(
            request.UserId, taskList, identity.Items, request.IsPrivate, cancellationToken);

        // An entry the shelf already answers is crossed off before the list is written, so it takes one
        // save rather than two - see StockedEntryCompletion, which reads nothing for the ordinary lists
        // this handler mostly saves. The owner's shelves, not the caller's: somebody editing through a
        // share is asking about the list's own storages.
        await _stockedEntryCompletion.CrossOffWhatTheShelfCoversAsync(
            taskList.UserId, identity.Items, cancellationToken);

        // A caller that said nothing about the description keeps the one that is stored. That is what
        // lets a client which has not learned about the field - the phone, an older tab - go on saving
        // lists without erasing what was written somewhere else.
        taskList.Update(
            request.Title, identity.Items, request.IsGroup, request.IsPrivate, request.EncryptedContent, request.Priority,
            request.Description ?? taskList.Description);

        // After Update, which rebuilds the items and therefore the derived half of completion. Said
        // only when the caller said it: null is "not provided", and a save from a client that has never
        // heard of this must not reopen a list somebody closed.
        if (request.Completion is { } completion)
        {
            taskList.SetCompletion(completion);
        }

        // One save when another list had to be renamed too, so a failure cannot leave two entries
        // claiming one id in the database - the state this exists to prevent.
        if (identity.ListsToSaveToo.Count > 0)
        {
            await _taskRepository.UpdateManyAsync([taskList, .. identity.ListsToSaveToo], cancellationToken);
        }
        else
        {
            await _taskRepository.UpdateAsync(taskList, cancellationToken);
        }

        // Crossing off a restock errand says the shelf was filled, so the shelf is filled - but the
        // entry stays, crossed off, rather than disappearing under the finger that just tapped it. The
        // checklist asks for a refresh a few minutes later and that is what clears it. Does nothing at
        // all for the ordinary lists this handler mostly saves.
        await _restockCompletion.TopUpFinishedAsync(request.Id, cancellationToken);

        if (placedOnInventoryId is { } inventoryId)
        {
            await _productEntryPlacement.SettleTheRestockListAsync(inventoryId, cancellationToken);
        }

        return EditOutcome.Success;
    }

    /// <summary>
    /// An entry whose description was not sent keeps the one it already carries - see
    /// UpdateTaskListCommand.EntriesKeepingTheirNotes. Its own pass rather than folded in with the
    /// categories or the product, because each is separately omitted: the phone sends categories and
    /// neither of the other two.
    /// </summary>
    /// <summary>
    /// An entry that said nothing about what it waits for keeps its steps - see
    /// UpdateTaskListCommand.EntriesKeepingTheirSteps.
    /// </summary>
    private static void KeepTheStepsOfEntriesThatSaidNothing(
        IReadOnlyList<TaskItem> incoming, TaskList stored, IReadOnlySet<Guid>? entriesKeepingTheirSteps)
    {
        if (entriesKeepingTheirSteps is not { Count: > 0 })
        {
            return;
        }

        var storedById = stored.Items.ToDictionary(item => item.Id);
        foreach (var item in incoming.Where(item => entriesKeepingTheirSteps.Contains(item.Id)))
        {
            if (storedById.TryGetValue(item.Id, out var storedItem))
            {
                item.KeepStepsOf(storedItem);
            }
        }
    }

    /// <summary>
    /// An entry that said nothing about how it is drawn or how much it matters keeps both - see
    /// UpdateTaskListCommand.EntriesKeepingTheirLook.
    /// </summary>
    private static void KeepTheLookOfEntriesThatSaidNothing(
        IReadOnlyList<TaskItem> incoming, TaskList stored, IReadOnlySet<Guid>? entriesKeepingTheirLook)
    {
        if (entriesKeepingTheirLook is not { Count: > 0 })
        {
            return;
        }

        var storedById = stored.Items.ToDictionary(item => item.Id);
        foreach (var item in incoming.Where(item => entriesKeepingTheirLook.Contains(item.Id)))
        {
            if (storedById.TryGetValue(item.Id, out var storedItem))
            {
                item.KeepLookOf(storedItem);
            }
        }
    }

    /// <summary>
    /// An entry that said nothing about the ways it is done by keeps them - see
    /// UpdateTaskListCommand.EntriesKeepingTheirAlternatives.
    /// </summary>
    private static void KeepTheAlternativesOfEntriesThatSaidNothing(
        IReadOnlyList<TaskItem> incoming, TaskList stored, IReadOnlySet<Guid>? entriesKeepingTheirAlternatives)
    {
        if (entriesKeepingTheirAlternatives is not { Count: > 0 })
        {
            return;
        }

        var storedById = stored.Items.ToDictionary(item => item.Id);
        foreach (var item in incoming.Where(item => entriesKeepingTheirAlternatives.Contains(item.Id)))
        {
            if (storedById.TryGetValue(item.Id, out var storedItem))
            {
                item.KeepAlternativesOf(storedItem);
            }
        }
    }

    private static void KeepTheDescriptionOfEntriesThatSaidNothing(
        IReadOnlyList<TaskItem> incoming, TaskList stored, IReadOnlySet<Guid>? entriesKeepingTheirNotes)
    {
        if (entriesKeepingTheirNotes is not { Count: > 0 })
        {
            return;
        }

        var storedById = stored.Items.ToDictionary(item => item.Id);
        foreach (var item in incoming.Where(item => entriesKeepingTheirNotes.Contains(item.Id)))
        {
            if (storedById.TryGetValue(item.Id, out var storedItem))
            {
                item.KeepNotesOf(storedItem);
            }
        }
    }

    /// <summary>
    /// An entry that sent no product keeps the one it already describes - see
    /// UpdateTaskListCommand.EntriesKeepingTheirProduct. Written apart from the categories rather than
    /// folded in with them, because the two are separately omitted: the phone sends categories and no
    /// product, and each has to be kept on its own.
    /// </summary>
    private static void KeepWhatEntriesThatSaidNothingAlreadyAskFor(
        IReadOnlyList<TaskItem> incoming, TaskList stored, IReadOnlySet<Guid>? entriesKeepingTheirProduct)
    {
        if (entriesKeepingTheirProduct is not { Count: > 0 })
        {
            return;
        }

        var storedById = stored.Items.ToDictionary(item => item.Id);
        foreach (var item in incoming.Where(item => entriesKeepingTheirProduct.Contains(item.Id)))
        {
            if (storedById.TryGetValue(item.Id, out var storedItem))
            {
                item.KeepProductOf(storedItem);
            }
        }
    }

    /// <summary>
    /// An entry whose categories were not sent keeps the ones it already has. The wire cannot tell
    /// "nothing to say" from "none at all" once it has become a TaskItem, so which entries meant which
    /// is decided where the request is read and travels on the command - see
    /// UpdateTaskListCommand.EntriesKeepingTheirCategories.
    /// </summary>
    private static void KeepTheCategoriesOfEntriesThatSaidNothing(
        IReadOnlyList<TaskItem> incoming, TaskList stored, IReadOnlySet<Guid>? entriesKeepingTheirCategories)
    {
        if (entriesKeepingTheirCategories is not { Count: > 0 })
        {
            return;
        }

        var storedById = stored.Items.ToDictionary(item => item.Id);
        foreach (var item in incoming.Where(item => entriesKeepingTheirCategories.Contains(item.Id)))
        {
            if (storedById.TryGetValue(item.Id, out var storedItem))
            {
                item.KeepCategoriesOf(storedItem);
            }
        }
    }
}
