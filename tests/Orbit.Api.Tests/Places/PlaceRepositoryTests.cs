using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Places;
using Orbit.Data;
using Orbit.Data.Repositories;
using Xunit;

namespace Orbit.Api.Tests.Places;

/// <summary>
/// The real <see cref="PlaceRepository"/> against a database rather than the in-memory double, because
/// the part worth proving is the part a double replaces: a place's belonging to lists is a table of its
/// own, and saving replaces those rows rather than diffing them - the shape that has gone wrong before
/// on the task lists (see TaskRepository's own comment about re-added rows).
///
/// SQLite stands in for PostgreSQL as it does in FolderRepositoryTests: what is under test is which rows
/// the method decides to touch, not anything provider-specific.
///
/// <b>The delta cursor is not among them.</b> Narrowing by UpdatedAtUtc is a DateTimeOffset comparison,
/// which SQLite's provider refuses to translate and PostgreSQL's takes without comment - so a test for
/// it here would be a test of the stand-in. The notes' own repository, which has had the same cursor
/// since long before this, is untested against SQLite for the same reason.
/// </summary>
public sealed class PlaceRepositoryTests : IDisposable
{
    private readonly TemporarySqliteDatabase _database = new();
    private readonly OrbitDbContext _dbContext;
    private static readonly Guid OwnerUserId = Guid.NewGuid();

    public PlaceRepositoryTests() => _dbContext = _database.DbContext;

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task A_place_comes_back_with_everything_it_was_stored_with()
    {
        var repository = new PlaceRepository(_dbContext);
        var onTheList = Guid.NewGuid();
        var place = Place.Create(
            OwnerUserId, "The good bakery", "Sourdough on Thursdays", Somewhere(), "#cc4a3f",
            ItemPriority.High, [onTheList], isPrivate: false);

        await repository.AddAsync(place, CancellationToken.None);

        var stored = await repository.GetByIdAsync(OwnerUserId, place.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("The good bakery", stored.Name);
        Assert.Equal("Sourdough on Thursdays", stored.Description);
        Assert.Equal("Piękna 1, Warszawa", stored.Where.Address);
        Assert.Equal(52.2297, stored.Where.Latitude);
        Assert.Equal("#cc4a3f", stored.Colour);
        Assert.Equal(ItemPriority.High, stored.Priority);
        Assert.Equal([onTheList], stored.TaskListIds);
    }

    /// <summary>
    /// The lists it belongs to are replaced wholesale on a save. Written down because the alternative -
    /// letting the change tracker work it out - is what produced "0 rows affected" on the task lists.
    /// </summary>
    [Fact]
    public async Task Saving_replaces_the_lists_a_place_belongs_to()
    {
        var repository = new PlaceRepository(_dbContext);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var place = Place.Create(OwnerUserId, "Bakery", "", Somewhere(), taskListIds: [first], isPrivate: false);
        await repository.AddAsync(place, CancellationToken.None);

        place.Update("Bakery", "", Somewhere(), "", ItemPriority.Normal, [second], isPrivate: false);
        await repository.UpdateAsync(place, CancellationToken.None);

        var stored = await repository.GetByIdAsync(OwnerUserId, place.Id, CancellationToken.None);
        Assert.Equal([second], stored!.TaskListIds);
    }

    /// <summary>An address nobody wrote comes back as none rather than as an empty line.</summary>
    [Fact]
    public async Task A_place_with_no_address_says_so_rather_than_saying_nothing()
    {
        var repository = new PlaceRepository(_dbContext);
        var place = Place.Create(OwnerUserId, "Where we park", "", new EventLocation(null, 52.1, 21.1), isPrivate: false);

        await repository.AddAsync(place, CancellationToken.None);

        var stored = await repository.GetByIdAsync(OwnerUserId, place.Id, CancellationToken.None);
        Assert.Null(stored!.Where.Address);
    }

    [Fact]
    public async Task Somebody_elses_place_is_neither_read_nor_deleted()
    {
        var repository = new PlaceRepository(_dbContext);
        var place = Place.Create(OwnerUserId, "Bakery", "", Somewhere(), isPrivate: false);
        await repository.AddAsync(place, CancellationToken.None);

        var otherUserId = Guid.NewGuid();
        Assert.Null(await repository.GetByIdAsync(otherUserId, place.Id, CancellationToken.None));
        Assert.False(await repository.DeleteAsync(otherUserId, place.Id, CancellationToken.None));
        Assert.NotNull(await repository.GetByIdAsync(OwnerUserId, place.Id, CancellationToken.None));
    }

    /// <summary>Deleting a place takes its belongings with it - the rows exist only for it.</summary>
    [Fact]
    public async Task Deleting_a_place_takes_the_lists_it_belonged_to_with_it()
    {
        var repository = new PlaceRepository(_dbContext);
        var place = Place.Create(OwnerUserId, "Bakery", "", Somewhere(), taskListIds: [Guid.NewGuid()], isPrivate: false);
        await repository.AddAsync(place, CancellationToken.None);

        Assert.True(await repository.DeleteAsync(OwnerUserId, place.Id, CancellationToken.None));

        Assert.Empty(_dbContext.Set<Orbit.Data.Entities.PlaceTaskListLinkEntity>());
    }

    private static EventLocation Somewhere() => new("Piękna 1, Warszawa", 52.2297, 21.0122);
}
