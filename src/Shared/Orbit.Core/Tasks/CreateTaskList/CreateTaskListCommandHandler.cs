using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;

namespace Orbit.Core.Tasks.CreateTaskList;

public sealed class CreateTaskListCommandHandler : IRequestHandler<CreateTaskListCommand, Guid>
{
    private readonly ITaskRepository _taskRepository;
    private readonly TaskListLinkValidator _taskListLinkValidator;
    private readonly ShelfUsage? _shelfUsage;

    /// <param name="shelfUsage">
    /// Recounts what the shelf items the new list's entries stand for are asked for - see ShelfUsage. Optional
    /// so a test about something else need not build one; the application always has it.
    /// </param>
    public CreateTaskListCommandHandler(
        ITaskRepository taskRepository, TaskListLinkValidator taskListLinkValidator, ShelfUsage? shelfUsage = null)
    {
        _taskRepository = taskRepository;
        _taskListLinkValidator = taskListLinkValidator;
        _shelfUsage = shelfUsage;
    }

    public async Task<Guid> HandleAsync(CreateTaskListCommand request, CancellationToken cancellationToken)
    {
        await _taskListLinkValidator.ValidateAsync(request.UserId, taskListId: null, request.Items, cancellationToken);

        // A new list can arrive carrying entries a client already named - see TaskItemIdentity. The list
        // itself has no id yet, so nothing of its own can be contested; what can is an entry whose id is
        // already living on one of this owner's other lists.
        var identity = TaskItemIdentity.Resolve(
            request.Items,
            await _taskRepository.GetHoldingItemsAsync(
                request.UserId, Guid.Empty, [.. request.Items.Select(item => item.Id)], cancellationToken));

        // Every entry of a new list is stored now for the first time - see TaskItem.CreatedAtUtc.
        TaskItemReferences.StampCreationTimes(identity.Items, [], DateTimeOffset.UtcNow);

        var taskList = TaskList.Create(
            request.UserId, request.Title, identity.Items, request.IsGroup, request.IsPrivate, request.EncryptedContent,
            request.Priority, description: request.Description ?? string.Empty, folderId: request.FolderId);
        // A list made on the Finished tab begins there - see TaskEditor's new-list defaults.
        taskList.SetCompletion(request.Completion);

        // An entry made as a reference joins its group - see TaskItemReferences - and what it says is
        // passed on to the rest of that group. The same precedence the update keeps for renamed lists.
        var changedByReferences = await new TaskItemReferences(_taskRepository).SettleAsync(
            request.UserId, taskList, new Dictionary<Guid, Guid?>(), new HashSet<Guid>(), new HashSet<Guid>(),
            cancellationToken);
        IReadOnlyList<TaskList> alsoSaved =
        [
            .. changedByReferences.Where(changed => identity.ListsToSaveToo.All(renamed => renamed.Id != changed.Id)),
            .. identity.ListsToSaveToo
        ];

        if (alsoSaved.Count > 0)
        {
            await _taskRepository.AddAsync(taskList, cancellationToken);
            await _taskRepository.UpdateManyAsync(alsoSaved, cancellationToken);
        }
        else
        {
            await _taskRepository.AddAsync(taskList, cancellationToken);
        }

        if (_shelfUsage is not null)
        {
            await _shelfUsage.RecountAsync(
                request.UserId, ShelfUsage.ShelfItemsOf([taskList, .. alsoSaved]), cancellationToken);
        }

        return taskList.Id;
    }
}
