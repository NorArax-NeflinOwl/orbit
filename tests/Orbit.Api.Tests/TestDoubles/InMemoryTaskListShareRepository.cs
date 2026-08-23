using Orbit.Core.Tasks;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="ITaskListShareRepository"/> stub for unit tests that need real add/lookup/update
/// behavior, including per-recipient scoping, without spinning up SQLite. Mirrors InMemoryCalendarEventShareRepository.
/// </summary>
internal sealed class InMemoryTaskListShareRepository : ITaskListShareRepository
{
    private readonly List<TaskListShare> _shares = [];

    public Task AddAsync(TaskListShare share, CancellationToken cancellationToken)
    {
        _shares.Add(share);
        return Task.CompletedTask;
    }

    public Task<TaskListShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_shares.FirstOrDefault(share => share.Id == id && share.RecipientUserId == recipientUserId));

    public Task<TaskListShare?> FindExistingAsync(Guid sourceTaskListId, Guid recipientUserId, CancellationToken cancellationToken)
        => Task.FromResult(_shares.FirstOrDefault(share => share.SourceTaskListId == sourceTaskListId && share.RecipientUserId == recipientUserId));

    public Task UpdateAsync(TaskListShare share, CancellationToken cancellationToken)
    {
        // Handlers mutate the same TaskListShare instance this repository already holds a reference to,
        // so there is nothing to replace here - mirrors InMemoryCalendarEventShareRepository.
        return Task.CompletedTask;
    }
}
