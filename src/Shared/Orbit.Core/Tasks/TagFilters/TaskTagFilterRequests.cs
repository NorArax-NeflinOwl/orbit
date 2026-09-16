using Orbit.Core.Abstractions;
using Orbit.Core.Tags;

namespace Orbit.Core.Tasks.TagFilters;

/// <summary>Every filter the caller has made for the Tasks card - see <see cref="TaskTagFilter"/>.</summary>
public sealed record GetTaskTagFiltersQuery(Guid UserId) : IRequest<IReadOnlyList<TaskTagFilter>>;

/// <summary>Makes a filter out of these tags, and answers it as stored.</summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record CreateTaskTagFilterCommand(Guid UserId, IReadOnlyList<string> Tags, bool MatchesAll)
    : IRequest<TaskTagFilter>;

/// <summary>Takes one of the caller's filters away. False when they have none by that id.</summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record DeleteTaskTagFilterCommand(Guid UserId, Guid FilterId) : IRequest<bool>;

public sealed class GetTaskTagFiltersQueryHandler(ITaskTagFilterRepository filters)
    : IRequestHandler<GetTaskTagFiltersQuery, IReadOnlyList<TaskTagFilter>>
{
    public Task<IReadOnlyList<TaskTagFilter>> HandleAsync(GetTaskTagFiltersQuery request, CancellationToken cancellationToken)
        => filters.GetAllAsync(request.UserId, cancellationToken);
}

public sealed class CreateTaskTagFilterCommandHandler(ITaskTagFilterRepository filters)
    : IRequestHandler<CreateTaskTagFilterCommand, TaskTagFilter>
{
    /// <summary>The most words one filter may hold - more than any menu entry could name readably.</summary>
    public const int MostTags = 20;

    /// <summary>
    /// Refused rather than tidied into something: a filter of no words finds nothing, and one of too
    /// many is a client's mistake worth hearing about. The words themselves are tidied as a list's are,
    /// so "Home" and "home" chosen together are one.
    /// </summary>
    public async Task<TaskTagFilter> HandleAsync(CreateTaskTagFilterCommand request, CancellationToken cancellationToken)
    {
        var tags = TagNames.Tidy(request.Tags);
        if (tags.Count == 0)
        {
            throw new InvalidRequestException("A filter needs at least one tag.");
        }

        if (tags.Count > MostTags)
        {
            throw new InvalidRequestException($"A filter holds at most {MostTags} tags.");
        }

        TagNames.OrRefuse(tags, "filter tag");

        var filter = new TaskTagFilter(Guid.NewGuid(), tags, request.MatchesAll, DateTimeOffset.UtcNow);
        await filters.AddAsync(request.UserId, filter, cancellationToken);
        return filter;
    }
}

public sealed class DeleteTaskTagFilterCommandHandler(ITaskTagFilterRepository filters)
    : IRequestHandler<DeleteTaskTagFilterCommand, bool>
{
    public Task<bool> HandleAsync(DeleteTaskTagFilterCommand request, CancellationToken cancellationToken)
        => filters.DeleteAsync(request.UserId, request.FilterId, cancellationToken);
}
