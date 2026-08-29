using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.AcceptTaskListShare;

public sealed class AcceptTaskListShareCommandHandler : IRequestHandler<AcceptTaskListShareCommand, bool>
{
    private readonly ITaskListShareRepository _taskListShareRepository;
    private readonly TaskListShareCascade _taskListShareCascade;

    public AcceptTaskListShareCommandHandler(
        ITaskListShareRepository taskListShareRepository, TaskListShareCascade taskListShareCascade)
    {
        _taskListShareRepository = taskListShareRepository;
        _taskListShareCascade = taskListShareCascade;
    }

    /// <summary>Mirrors Orbit.Core.Notes.AcceptNoteShare.AcceptNoteShareCommandHandler - see its class comment.</summary>
    public async Task<bool> HandleAsync(AcceptTaskListShareCommand request, CancellationToken cancellationToken)
    {
        var share = await _taskListShareRepository.GetByIdAsync(request.RecipientUserId, request.ShareId, cancellationToken);
        if (share is null)
        {
            return false;
        }

        if (!share.IsAccepted)
        {
            share.MarkAccepted();
            await _taskListShareRepository.UpdateAsync(share, cancellationToken);
        }

        // The lists this one gathers, and its inventory, were granted alongside it and are accepted
        // with it - see TaskListShareCascade. Run for an already-accepted share too, so a tree that
        // grew after the offer was answered still opens rather than needing a second answer.
        await _taskListShareCascade.AcceptAsync(
            share.OwnerUserId, share.SourceTaskListId, request.RecipientUserId, cancellationToken);
        return true;
    }
}
