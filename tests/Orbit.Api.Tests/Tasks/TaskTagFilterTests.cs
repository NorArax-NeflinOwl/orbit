using Orbit.Core.Abstractions;
using Orbit.Core.Tags;
using Orbit.Core.Tasks.TagFilters;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// The filters an account makes for the dashboard's Tasks card - see TaskTagFilter. What matters is what a
/// client leans on without checking: any tag unless told every, a tag known whatever its case, and a
/// filter of no words refused rather than stored to find nothing.
/// </summary>
public sealed class TaskTagFilterTests
{
    [Fact]
    public void Any_one_tag_is_enough_unless_every_one_is_asked_for()
    {
        var any = new TaskTagFilter(Guid.NewGuid(), ["home", "shopping"], MatchesAll: false, DateTimeOffset.UtcNow);
        var every = any with { MatchesAll = true };

        Assert.True(any.Matches(["shopping"]));
        Assert.False(every.Matches(["shopping"]));
        Assert.True(every.Matches(["Shopping", "work", "HOME"]));
        Assert.False(any.Matches([]));
    }

    [Fact]
    public void A_filter_is_called_by_its_tags()
    {
        var filter = new TaskTagFilter(Guid.NewGuid(), ["home", "shopping"], MatchesAll: false, DateTimeOffset.UtcNow);

        Assert.Equal("home or shopping", filter.Describe("or", "and"));
        Assert.Equal("home and shopping", (filter with { MatchesAll = true }).Describe("or", "and"));
    }

    [Fact]
    public async Task Made_filters_are_tidied_kept_per_account_and_can_be_taken_away()
    {
        var repository = new InMemoryTaskTagFilterRepository();
        var me = Guid.NewGuid();

        var made = await new CreateTaskTagFilterCommandHandler(repository).HandleAsync(
            new CreateTaskTagFilterCommand(me, [" Home ", "home", "", "shopping"], MatchesAll: true), CancellationToken.None);

        Assert.Equal(["Home", "shopping"], made.Tags);
        var mine = await new GetTaskTagFiltersQueryHandler(repository).HandleAsync(new GetTaskTagFiltersQuery(me), CancellationToken.None);
        Assert.Equal(made.Id, Assert.Single(mine).Id);
        Assert.Empty(await repository.GetAllAsync(Guid.NewGuid(), CancellationToken.None));

        var deleter = new DeleteTaskTagFilterCommandHandler(repository);
        Assert.False(await deleter.HandleAsync(new DeleteTaskTagFilterCommand(Guid.NewGuid(), made.Id), CancellationToken.None));
        Assert.True(await deleter.HandleAsync(new DeleteTaskTagFilterCommand(me, made.Id), CancellationToken.None));
        Assert.Empty(await repository.GetAllAsync(me, CancellationToken.None));
    }

    [Fact]
    public async Task A_filter_of_no_tags_is_refused()
    {
        var handler = new CreateTaskTagFilterCommandHandler(new InMemoryTaskTagFilterRepository());

        await Assert.ThrowsAsync<InvalidRequestException>(() => handler.HandleAsync(
            new CreateTaskTagFilterCommand(Guid.NewGuid(), [" ", ""], MatchesAll: false), CancellationToken.None));
    }

    private sealed class InMemoryTaskTagFilterRepository : ITaskTagFilterRepository
    {
        private readonly List<(Guid UserId, TaskTagFilter Filter)> _filters = [];

        public Task<IReadOnlyList<TaskTagFilter>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<TaskTagFilter>>(
                [.. _filters.Where(entry => entry.UserId == userId).Select(entry => entry.Filter)]);

        public Task AddAsync(Guid userId, TaskTagFilter filter, CancellationToken cancellationToken)
        {
            _filters.Add((userId, filter));
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Guid userId, Guid filterId, CancellationToken cancellationToken)
            => Task.FromResult(_filters.RemoveAll(entry => entry.UserId == userId && entry.Filter.Id == filterId) > 0);
    }
}
