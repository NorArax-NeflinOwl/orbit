namespace Orbit.Contracts.Tasks;

/// <summary>
/// A filter for the dashboard's Tasks card, made of the account's list tags - see
/// Orbit.Core.Tasks.TagFilters.TaskTagFilter.
/// </summary>
/// <param name="MatchesAll">Every tag has to be on a list, rather than any one of them.</param>
public sealed record TaskTagFilterDto(Guid Id, IReadOnlyList<string> Tags, bool MatchesAll, DateTimeOffset CreatedAtUtc);

/// <summary>Makes a filter - POST /api/task-filters.</summary>
public sealed record CreateTaskTagFilterRequest(IReadOnlyList<string> Tags, bool MatchesAll);
