using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Places;
using Orbit.Core.Places.CreatePlace;
using Orbit.Core.Places.DeletePlace;
using Orbit.Core.Places.DuplicatePlace;
using Orbit.Core.Places.GetPlaceById;
using Orbit.Core.Places.GetPlaces;
using Orbit.Core.Places.UpdatePlace;
using Orbit.Core.Sync;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Places;

/// <summary>
/// Somewhere on the map worth keeping - see Orbit.Core.Places.Place. A place has one owner for its whole
/// life, and everything else is reached through a share of it, so most of what these hold in place is
/// that a request for somebody else's answers the same way as one for an id that never existed.
/// </summary>
public sealed class PlaceCommandHandlerTests
{
    private readonly InMemoryPlaceRepository _places = new();
    private readonly InMemoryPlaceShareRepository _shares = new();
    private readonly InMemoryUserRepository _users = new();

    /// <summary>Who may see what, which every handler here now asks before it does anything.</summary>
    private PlaceAccessResolver Access => new(_places, _shares, _users);

    [Fact]
    public async Task A_place_is_made_with_everything_the_form_said()
    {
        var repository = _places;
        var userId = Guid.NewGuid();
        var onTheList = Guid.NewGuid();

        var id = await new CreatePlaceCommandHandler(repository).HandleAsync(
            new CreatePlaceCommand(
                userId, "The good bakery", "Sourdough on Thursdays", Somewhere(), "#cc4a3f",
                ItemPriority.High, [onTheList], IsPrivate: false),
            CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("The good bakery", stored.Name);
        Assert.Equal("Sourdough on Thursdays", stored.Description);
        Assert.Equal("#cc4a3f", stored.Colour);
        Assert.Equal(ItemPriority.High, stored.Priority);
        Assert.Equal([onTheList], stored.TaskListIds);
        Assert.Equal(52.2297, stored.Where.Latitude);
    }

    /// <summary>Naming the same list twice is one belonging written twice, not two.</summary>
    [Fact]
    public async Task A_place_belongs_to_a_list_once_however_often_it_is_named()
    {
        var repository = _places;
        var userId = Guid.NewGuid();
        var onTheList = Guid.NewGuid();

        var id = await new CreatePlaceCommandHandler(repository).HandleAsync(
            new CreatePlaceCommand(
                userId, "Bakery", "", Somewhere(), TaskListIds: [onTheList, onTheList], IsPrivate: false),
            CancellationToken.None);

        var stored = await repository.GetByIdAsync(userId, id, CancellationToken.None);
        Assert.Equal([onTheList], stored!.TaskListIds);
    }

    [Fact]
    public async Task Saving_a_place_writes_what_changed()
    {
        var repository = _places;
        var userId = Guid.NewGuid();
        var place = Place.Create(userId, "Bakery", "", Somewhere(), isPrivate: false);
        await repository.AddAsync(place, CancellationToken.None);

        var saved = await new UpdatePlaceCommandHandler(Access, repository).HandleAsync(
            new UpdatePlaceCommand(
                userId, place.Id, "The good bakery", "Sourdough on Thursdays", Somewhere(), "#3f9a56",
                ItemPriority.Low, null, IsPrivate: false),
            CancellationToken.None);

        Assert.True(saved);
        var stored = await repository.GetByIdAsync(userId, place.Id, CancellationToken.None);
        Assert.Equal("The good bakery", stored!.Name);
        Assert.Equal(ItemPriority.Low, stored.Priority);
        Assert.Empty(stored.TaskListIds);
    }

    /// <summary>A place a list's Location entry made remembers which entry - see Place.SourceTaskItemId.</summary>
    [Fact]
    public async Task A_place_made_from_an_entry_remembers_which()
    {
        var userId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        var id = await new CreatePlaceCommandHandler(_places).HandleAsync(
            new CreatePlaceCommand(
                userId, "The new flat", "", Somewhere(), TaskListIds: [Guid.NewGuid()], IsPrivate: false,
                SourceTaskItemId: entryId),
            CancellationToken.None);

        Assert.Equal(entryId, (await _places.GetByIdAsync(userId, id, CancellationToken.None))!.SourceTaskItemId);
    }

    /// <summary>
    /// And a save that does not say - the phone's, which knows nothing about entries' places - leaves it
    /// answering to the same one, rather than cutting it loose so the next list save makes a second.
    /// </summary>
    [Fact]
    public async Task Saving_a_place_without_naming_an_entry_keeps_the_one_it_came_from()
    {
        var userId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var place = Place.Create(userId, "The new flat", "", Somewhere(), isPrivate: false, sourceTaskItemId: entryId);
        await _places.AddAsync(place, CancellationToken.None);

        await new UpdatePlaceCommandHandler(Access, _places).HandleAsync(
            new UpdatePlaceCommand(
                userId, place.Id, "The new flat", "", Somewhere(), "#3f9a56", ItemPriority.Low, null, IsPrivate: false),
            CancellationToken.None);

        Assert.Equal(entryId, (await _places.GetByIdAsync(userId, place.Id, CancellationToken.None))!.SourceTaskItemId);
    }

    /// <summary>
    /// A copy is somebody keeping that spot on purpose, so it answers to no entry: removing the entry
    /// takes its own place away and leaves the copy.
    /// </summary>
    [Fact]
    public async Task A_copy_of_a_place_an_entry_made_answers_to_no_entry()
    {
        var userId = Guid.NewGuid();
        var place = Place.Create(userId, "The new flat", "", Somewhere(), isPrivate: false, sourceTaskItemId: Guid.NewGuid());
        await _places.AddAsync(place, CancellationToken.None);

        var copyId = await new DuplicatePlaceCommandHandler(Access, _places).HandleAsync(
            new DuplicatePlaceCommand(userId, place.Id, "The new flat (copy)"), CancellationToken.None);

        Assert.Null((await _places.GetByIdAsync(userId, copyId!.Value, CancellationToken.None))!.SourceTaskItemId);
    }

    [Fact]
    public async Task Somebody_elses_place_cannot_be_saved()
    {
        var repository = _places;
        var ownerId = Guid.NewGuid();
        var place = Place.Create(ownerId, "Bakery", "", Somewhere(), isPrivate: false);
        await repository.AddAsync(place, CancellationToken.None);

        var saved = await new UpdatePlaceCommandHandler(Access, repository).HandleAsync(
            new UpdatePlaceCommand(Guid.NewGuid(), place.Id, "Mine now", "", Somewhere(), IsPrivate: false),
            CancellationToken.None);

        Assert.False(saved);
        Assert.Equal("Bakery", (await repository.GetByIdAsync(ownerId, place.Id, CancellationToken.None))!.Name);
    }

    /// <summary>
    /// The tombstone is the point: a client holding its own copy learns the place is gone, which a delta
    /// cannot say on its own - see SyncTombstone.
    /// </summary>
    [Fact]
    public async Task Deleting_a_place_leaves_a_tombstone_behind_it()
    {
        var repository = _places;
        var tombstones = new InMemorySyncTombstoneRepository();
        var userId = Guid.NewGuid();
        var place = Place.Create(userId, "Bakery", "", Somewhere(), isPrivate: false);
        await repository.AddAsync(place, CancellationToken.None);

        var deleted = await new DeletePlaceCommandHandler(repository, _shares, tombstones).HandleAsync(
            new DeletePlaceCommand(userId, place.Id), CancellationToken.None);

        Assert.True(deleted);
        Assert.Null(await repository.GetByIdAsync(userId, place.Id, CancellationToken.None));
        var tombstone = Assert.Single(tombstones.Tombstones);
        Assert.Equal(SyncEntityType.Place, tombstone.EntityType);
        Assert.Equal(place.Id, tombstone.EntityId);
    }

    /// <summary>And nothing is written for a delete that deleted nothing.</summary>
    [Fact]
    public async Task Deleting_somebody_elses_place_leaves_no_tombstone()
    {
        var repository = _places;
        var tombstones = new InMemorySyncTombstoneRepository();
        var place = Place.Create(Guid.NewGuid(), "Bakery", "", Somewhere(), isPrivate: false);
        await repository.AddAsync(place, CancellationToken.None);

        var deleted = await new DeletePlaceCommandHandler(repository, _shares, tombstones).HandleAsync(
            new DeletePlaceCommand(Guid.NewGuid(), place.Id), CancellationToken.None);

        Assert.False(deleted);
        Assert.Empty(tombstones.Tombstones);
    }

    /// <summary>
    /// A copy is a second place in the same spot, with everything about it - the lists it belongs to
    /// included, because a copy of the bakery is still the shopping list's bakery.
    /// </summary>
    [Fact]
    public async Task A_copy_stands_in_the_same_spot_and_on_the_same_lists()
    {
        var repository = _places;
        var userId = Guid.NewGuid();
        var onTheList = Guid.NewGuid();
        var place = Place.Create(userId, "Bakery", "Sourdough", Somewhere(), "#cc4a3f", ItemPriority.High, [onTheList], isPrivate: false);
        await repository.AddAsync(place, CancellationToken.None);

        var copyId = await new DuplicatePlaceCommandHandler(Access, repository).HandleAsync(
            new DuplicatePlaceCommand(userId, place.Id, "Bakery (copy)"), CancellationToken.None);

        Assert.NotNull(copyId);
        Assert.NotEqual(place.Id, copyId);
        var copy = await repository.GetByIdAsync(userId, copyId!.Value, CancellationToken.None);
        Assert.Equal("Bakery (copy)", copy!.Name);
        Assert.Equal("Sourdough", copy.Description);
        Assert.Equal([onTheList], copy.TaskListIds);
        Assert.Equal(place.Where.Latitude, copy.Where.Latitude);
    }

    [Fact]
    public async Task Somebody_elses_place_cannot_be_copied()
    {
        var repository = _places;
        var place = Place.Create(Guid.NewGuid(), "Bakery", "", Somewhere(), isPrivate: false);
        await repository.AddAsync(place, CancellationToken.None);

        var copyId = await new DuplicatePlaceCommandHandler(Access, repository).HandleAsync(
            new DuplicatePlaceCommand(Guid.NewGuid(), place.Id), CancellationToken.None);

        Assert.Null(copyId);
    }

    /// <summary>A reader is told about their own places and nobody else's.</summary>
    [Fact]
    public async Task Only_this_accounts_places_come_back()
    {
        var repository = _places;
        var userId = Guid.NewGuid();
        await repository.AddAsync(Place.Create(userId, "Mine", "", Somewhere(), isPrivate: false), CancellationToken.None);
        await repository.AddAsync(Place.Create(Guid.NewGuid(), "Theirs", "", Somewhere(), isPrivate: false), CancellationToken.None);

        var mine = await new GetPlacesQueryHandler(Access).HandleAsync(
            new GetPlacesQuery(userId), CancellationToken.None);

        Assert.Equal("Mine", Assert.Single(mine).Name);
        Assert.Null(await new GetPlaceByIdQueryHandler(Access).HandleAsync(
            new GetPlaceByIdQuery(userId, Guid.NewGuid()), CancellationToken.None));
    }

    private static EventLocation Somewhere() => new("Piękna 1, Warszawa", 52.2297, 21.0122);
}
