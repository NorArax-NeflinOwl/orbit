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

        // Clients name their own entries now, so two of them can hand over the same id. Both sides are
        // renamed when they do - see TaskItemIdentity for why neither may keep it.
        var identity = TaskItemIdentity.Resolve(
            request.Items,
            await _taskRepository.GetHoldingItemsAsync(
                taskList.UserId, request.Id, [.. request.Items.Select(item => item.Id)], cancellationToken));

        // A caller that said nothing about the description keeps the one that is stored. That is what
        // lets a client which has not learned about the field - the phone, an older tab - go on saving
        // lists without erasing what was written somewhere else.
        taskList.Update(
            request.Title, identity.Items, request.IsGroup, request.IsPrivate, request.EncryptedContent, request.Priority,
            request.Description ?? taskList.Description);

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

        return EditOutcome.Success;
    }
}
