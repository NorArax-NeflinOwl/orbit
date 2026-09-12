using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Tasks;
using Orbit.Data.Repositories;
using Xunit;

namespace Orbit.Api.Tests.Suggestions;

/// <summary>
/// What a suggested name is the name of - NameSuggestionRepository.FindSourcesAsync - against a real
/// database. The similarity search itself is PostgreSQL's and cannot run here; this lookup is plain SQL on
/// purpose, so it can.
/// </summary>
public sealed class NameSuggestionSourcesTests : IDisposable
{
    private readonly TemporarySqliteDatabase _database = new();
    private readonly Guid _userId = Guid.NewGuid();

    /// <summary>
    /// The members of a reference group all say the same thing, so the group is offered once - and what a
    /// pick points at is the source, never a member, even when the name was found on the member.
    /// </summary>
    [Fact]
    public async Task A_reference_group_is_offered_once_and_points_at_its_source()
    {
        var tasks = new TaskRepository(_database.DbContext);
        var source = TaskItem.Create("Sauce", null, false);
        await tasks.AddAsync(TaskList.Create(_userId, "Pasta", [source]), CancellationToken.None);
        var reference = TaskItem.Create("sauce", null, false, referencesTaskItemId: source.Id);
        await tasks.AddAsync(TaskList.Create(_userId, "Burger", [reference]), CancellationToken.None);

        var found = await new NameSuggestionRepository(_database.DbContext)
            .FindSourcesAsync(_userId, ["Sauce"], CancellationToken.None);

        var only = Assert.Single(found);
        Assert.Equal(source.Id, only.Id);
        Assert.Equal(source.Id, only.ItemId);
        Assert.Equal("Pasta", only.ContainerName);
    }

    [Fact]
    public async Task Nothing_of_somebody_elses_is_offered()
    {
        var tasks = new TaskRepository(_database.DbContext);
        await tasks.AddAsync(TaskList.Create(Guid.NewGuid(), "Theirs", [TaskItem.Create("Sauce", null, false)]), CancellationToken.None);

        var found = await new NameSuggestionRepository(_database.DbContext)
            .FindSourcesAsync(_userId, ["Sauce"], CancellationToken.None);

        Assert.Empty(found);
    }

    public void Dispose() => _database.Dispose();
}
