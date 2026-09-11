using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.CreateTaskList;

public sealed class CreateTaskListCommandHandler : IRequestHandler<CreateTaskListCommand, Guid>
{
    private readonly ITaskRepository _taskRepository;
    private readonly TaskListLinkValidator _taskListLinkValidator;

    public CreateTaskListCommandHandler(ITaskRepository taskRepository, TaskListLinkValidator taskListLinkValidator)
    {
        _taskRepository = taskRepository;
        _taskListLinkValidator = taskListLinkValidator;
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

        // Entries can arrive already ticked - a list written offline and pushed whole, or one made from
        // another. There is nothing stored to keep a time from, so a tick that came without one is
        // recorded as of now - see TaskItem.RecordWhenItWasDone.
        var nowUtc = DateTimeOffset.UtcNow;
        foreach (var item in identity.Items)
        {
            item.RecordWhenItWasDone(stored: null, nowUtc);
        }

        var taskList = TaskList.Create(
            request.UserId, request.Title, identity.Items, request.IsGroup, request.IsPrivate, request.EncryptedContent,
            request.Priority, description: request.Description ?? string.Empty, folderId: request.FolderId);
        // A list made on the Finished tab begins there - see TaskEditor's new-list defaults.
        taskList.SetCompletion(request.Completion);

        if (identity.ListsToSaveToo.Count > 0)
        {
            await _taskRepository.AddAsync(taskList, cancellationToken);
            await _taskRepository.UpdateManyAsync(identity.ListsToSaveToo, cancellationToken);
        }
        else
        {
            await _taskRepository.AddAsync(taskList, cancellationToken);
        }

        return taskList.Id;
    }
}
