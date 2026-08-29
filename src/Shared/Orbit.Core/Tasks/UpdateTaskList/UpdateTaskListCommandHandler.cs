using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;

namespace Orbit.Core.Tasks.UpdateTaskList;

public sealed class UpdateTaskListCommandHandler : IRequestHandler<UpdateTaskListCommand, EditOutcome>
{
    private readonly TaskListAccessResolver _taskListAccessResolver;
    private readonly ITaskRepository _taskRepository;
    private readonly TaskListLinkValidator _taskListLinkValidator;
    private readonly RestockCompletion _restockCompletion;

    public UpdateTaskListCommandHandler(
        TaskListAccessResolver taskListAccessResolver, ITaskRepository taskRepository,
        TaskListLinkValidator taskListLinkValidator, RestockCompletion restockCompletion)
    {
        _taskListAccessResolver = taskListAccessResolver;
        _taskRepository = taskRepository;
        _taskListLinkValidator = taskListLinkValidator;
        _restockCompletion = restockCompletion;
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

        // Read before the update, since afterwards there is nothing left to compare against.
        var alreadyDone = taskList.Items.Where(item => item.IsCompleted).Select(item => item.Id).ToHashSet();

        taskList.Update(
            request.Title, request.Items, request.IsGroup, request.IsPrivate, request.EncryptedContent, request.Priority);
        await _taskRepository.UpdateAsync(taskList, cancellationToken);

        // Crossing off a restock errand says the shelf was filled - see RestockCompletion, which does
        // nothing at all for the ordinary lists this handler mostly saves.
        var justDone = taskList.Items
            .Where(item => item.IsCompleted && !alreadyDone.Contains(item.Id))
            .Select(item => item.Id)
            .ToList();
        await _restockCompletion.ApplyAsync(request.Id, justDone, cancellationToken);

        return EditOutcome.Success;
    }
}
