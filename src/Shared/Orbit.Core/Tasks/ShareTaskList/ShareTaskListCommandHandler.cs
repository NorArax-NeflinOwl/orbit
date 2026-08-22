using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.ShareTaskList;

public sealed class ShareTaskListCommandHandler : IRequestHandler<ShareTaskListCommand, Guid?>
{
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskListShareRepository _taskListShareRepository;

    public ShareTaskListCommandHandler(ITaskRepository taskRepository, ITaskListShareRepository taskListShareRepository)
    {
        _taskRepository = taskRepository;
        _taskListShareRepository = taskListShareRepository;
    }

    public async Task<Guid?> HandleAsync(ShareTaskListCommand request, CancellationToken cancellationToken)
    {
        var sourceTaskList = await _taskRepository.GetByIdAsync(request.OwnerUserId, request.TaskListId, cancellationToken);
        if (sourceTaskList is null)
        {
            return null;
        }

        var share = TaskListShare.Create(sourceTaskList.Id, request.OwnerUserId, request.RecipientUserId, request.AccessLevel);
        await _taskListShareRepository.AddAsync(share, cancellationToken);
        return share.Id;
    }
}
