using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.AcceptTaskListShare;

public sealed class AcceptTaskListShareCommandHandler : IRequestHandler<AcceptTaskListShareCommand, bool>
{
    private readonly ITaskListShareRepository _taskListShareRepository;

    public AcceptTaskListShareCommandHandler(ITaskListShareRepository taskListShareRepository)
    {
        _taskListShareRepository = taskListShareRepository;
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

        return true;
    }
}
