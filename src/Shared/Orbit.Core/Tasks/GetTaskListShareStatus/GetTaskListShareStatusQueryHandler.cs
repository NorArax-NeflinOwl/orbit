using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.GetTaskListShareStatus;

public sealed class GetTaskListShareStatusQueryHandler : IRequestHandler<GetTaskListShareStatusQuery, bool?>
{
    private readonly ITaskListShareRepository _taskListShareRepository;

    public GetTaskListShareStatusQueryHandler(ITaskListShareRepository taskListShareRepository)
    {
        _taskListShareRepository = taskListShareRepository;
    }

    public async Task<bool?> HandleAsync(GetTaskListShareStatusQuery request, CancellationToken cancellationToken)
    {
        var share = await _taskListShareRepository.GetByIdAsync(request.RecipientUserId, request.ShareId, cancellationToken);
        return share?.IsAccepted;
    }
}
