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

        var taskList = TaskList.Create(
            request.UserId, request.Title, identity.Items, request.IsGroup, request.IsPrivate, request.EncryptedContent,
            request.Priority);

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
