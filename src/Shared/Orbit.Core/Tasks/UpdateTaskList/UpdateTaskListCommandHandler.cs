using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.UpdateTaskList;

public sealed class UpdateTaskListCommandHandler : IRequestHandler<UpdateTaskListCommand, EditOutcome>
{
    private readonly TaskListAccessResolver _taskListAccessResolver;
    private readonly ITaskRepository _taskRepository;
    private readonly TaskListLinkValidator _taskListLinkValidator;

    public UpdateTaskListCommandHandler(
        TaskListAccessResolver taskListAccessResolver, ITaskRepository taskRepository, TaskListLinkValidator taskListLinkValidator)
    {
        _taskListAccessResolver = taskListAccessResolver;
        _taskRepository = taskRepository;
        _taskListLinkValidator = taskListLinkValidator;
    }

    /// <summary>Mirrors Orbit.Core.Notes.UpdateNote.UpdateNoteCommandHandler - see its class comment for what NotFound/Locked mean here.</summary>
    public async Task<EditOutcome> HandleAsync(UpdateTaskListCommand request, CancellationToken cancellationToken)
    {
        var taskList = await _taskListAccessResolver.ResolveAsync(request.UserId, request.Id, cancellationToken);
        if (taskList is null || !taskList.AccessLevel.AllowsEditing())
        {
            return EditOutcome.NotFound;
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

        taskList.Update(request.Title, request.Items, request.IsGroup, request.IsPrivate, request.EncryptedContent);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);
        return EditOutcome.Success;
    }
}
