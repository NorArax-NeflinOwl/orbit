namespace Orbit.Core.Tasks.TagFilters;

/// <summary>An account's filters for the dashboard's Tasks card - see <see cref="TaskTagFilter"/>.</summary>
public interface ITaskTagFilterRepository
{
    /// <summary>Every filter this account has made, oldest first - the order a menu lists them in.</summary>
    Task<IReadOnlyList<TaskTagFilter>> GetAllAsync(Guid userId, CancellationToken cancellationToken);

    Task AddAsync(Guid userId, TaskTagFilter filter, CancellationToken cancellationToken);

    /// <summary>Takes one away. False when this account has no filter by that id.</summary>
    Task<bool> DeleteAsync(Guid userId, Guid filterId, CancellationToken cancellationToken);
}
