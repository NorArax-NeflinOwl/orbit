namespace Orbit.Data.Entities;

/// <summary>
/// A filter an account made for the dashboard's Tasks card - see Orbit.Core.Tasks.TagFilters.TaskTagFilter.
/// An account setting beside OS_TAGS_COLOURS, and readable for the same reason that one is: the tags are
/// named here in the clear, so a filter made of tags used only on private lists names them readably.
/// </summary>
public sealed class TaskTagFilterEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>JSON-encoded list of the filter's tags - the same shape TaskEntity.TagsJson has.</summary>
    public string TagsJson { get; set; } = "[]";

    /// <summary>Every tag has to be on a list, rather than any one of them.</summary>
    public bool MatchesAll { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
