using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Notifications;
using Orbit.Core.Places;
using Orbit.Core.Places.AcceptPlaceShare;
using Orbit.Core.Places.DeletePlace;
using Orbit.Core.Places.DuplicatePlace;
using Orbit.Core.Places.GetPlaceById;
using Orbit.Core.Places.GetPlaces;
using Orbit.Core.Places.SharePlace;
using Orbit.Core.Places.UpdatePlace;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Places;

/// <summary>
/// Handing a place to somebody. It grants access to the one row rather than making a copy - the rule
/// every other kind of share here follows - so what these hold in place is that an offer does nothing
/// until it is taken up, that a reader who was given one may not rewrite it, and that getting rid of
/// one they were given takes it off their own map and nobody else's.
/// </summary>
public sealed class PlaceSharingTests
{
    private static readonly Guid OwnerUserId = Guid.NewGuid();
    private static readonly Guid RecipientUserId = Guid.NewGuid();

    private readonly InMemoryPlaceRepository _places = new();
    private readonly InMemoryPlaceShareRepository _shares = new();
    private readonly InMemoryUserRepository _users = new();
    private readonly RecordingSharedItemNotifier _notifier = new();

    private PlaceAccessResolver Access => new(_places, _shares, _users);

    private async Task<Place> APlaceAsync(string name = "The good bakery")
    {
        var place = Place.Create(OwnerUserId, name, "Sourdough on Thursdays", Somewhere());
        await _places.AddAsync(place, CancellationToken.None);
        return place;
    }

    private Task<ShareOutcome?> ShareAsync(Guid placeId, ShareAccessLevel level = ShareAccessLevel.ReadOnly)
        => new SharePlaceCommandHandler(Access, _shares, _notifier).HandleAsync(
            new SharePlaceCommand(OwnerUserId, placeId, RecipientUserId, level), CancellationToken.None);

    private Task<bool> AcceptAsync(Guid shareId)
        => new AcceptPlaceShareCommandHandler(_shares).HandleAsync(
            new AcceptPlaceShareCommand(RecipientUserId, shareId), CancellationToken.None);

    /// <summary>
    /// An offer, not a grant. Until it is taken up the place is not on the recipient's map - which is
    /// what stops somebody's map filling with places they never said yes to.
    /// </summary>
    [Fact]
    public async Task An_offer_puts_nothing_on_the_other_persons_map_until_they_take_it_up()
    {
        var place = await APlaceAsync();

        var outcome = await ShareAsync(place.Id);

        Assert.NotNull(outcome);
        Assert.False(outcome.AlreadyShared);
        Assert.Empty(await new GetPlacesQueryHandler(Access).HandleAsync(
            new GetPlacesQuery(RecipientUserId), CancellationToken.None));

        Assert.True(await AcceptAsync(outcome.ShareId));

        var theirs = Assert.Single(await new GetPlacesQueryHandler(Access).HandleAsync(
            new GetPlacesQuery(RecipientUserId), CancellationToken.None));
        Assert.Equal("The good bakery", theirs.Name);
        Assert.True(theirs.IsShared);
        Assert.Equal(ShareAccessLevel.ReadOnly, theirs.AccessLevel);
    }

    /// <summary>The person who keeps it is told somebody else holds it - the other side of the same row.</summary>
    [Fact]
    public async Task The_owner_is_told_their_place_is_in_somebody_elses_hands()
    {
        var place = await APlaceAsync();
        await AcceptAsync((await ShareAsync(place.Id))!.ShareId);

        var mine = Assert.Single(await new GetPlacesQueryHandler(Access).HandleAsync(
            new GetPlacesQuery(OwnerUserId), CancellationToken.None));

        Assert.False(mine.IsShared);
        Assert.True(mine.IsSharedWithOthers);
    }

    /// <summary>
    /// The invitation is what the recipient presses Accept on, so the notification has to name the share
    /// rather than the place - and it says "place" rather than "location", which is somebody's position.
    /// </summary>
    [Fact]
    public async Task Sharing_a_place_tells_the_person_it_was_offered_to()
    {
        var place = await APlaceAsync();

        var outcome = await ShareAsync(place.Id);

        var told = Assert.Single(_notifier.Announced);
        Assert.Equal(RecipientUserId, told.RecipientUserId);
        Assert.Equal(SharedItemKind.Place, told.Kind);
        Assert.Equal("The good bakery", told.ItemTitle);
        Assert.Equal(outcome!.ShareId, told.Link.PendingShareId);
    }

    /// <summary>Read-only means read-only: what was handed over to look at is not theirs to rewrite.</summary>
    [Fact]
    public async Task A_place_handed_over_to_read_cannot_be_rewritten()
    {
        var place = await APlaceAsync();
        await AcceptAsync((await ShareAsync(place.Id))!.ShareId);

        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            new UpdatePlaceCommandHandler(Access, _places).HandleAsync(
                new UpdatePlaceCommand(
                    RecipientUserId, place.Id, "Not the bakery", "", Somewhere(), "", ItemPriority.Normal, null),
                CancellationToken.None));
    }

    [Fact]
    public async Task A_place_handed_over_to_edit_can_be_rewritten()
    {
        var place = await APlaceAsync();
        await AcceptAsync((await ShareAsync(place.Id, ShareAccessLevel.CanEdit))!.ShareId);

        var saved = await new UpdatePlaceCommandHandler(Access, _places).HandleAsync(
            new UpdatePlaceCommand(
                RecipientUserId, place.Id, "The very good bakery", "", Somewhere(), "", ItemPriority.Normal, null),
            CancellationToken.None);

        Assert.True(saved);
        var stored = await _places.GetByIdAsync(OwnerUserId, place.Id, CancellationToken.None);
        Assert.Equal("The very good bakery", stored!.Name);
    }

    /// <summary>
    /// Keeping one for yourself is what a reader does with a place they were shown. The copy is theirs,
    /// and it must not turn up as a second place on the map of the person who shared it.
    /// </summary>
    [Fact]
    public async Task A_place_somebody_was_shown_can_be_kept_as_their_own()
    {
        var place = await APlaceAsync();
        await AcceptAsync((await ShareAsync(place.Id))!.ShareId);

        var copyId = await new DuplicatePlaceCommandHandler(Access, _places).HandleAsync(
            new DuplicatePlaceCommand(RecipientUserId, place.Id, "The bakery they showed me"),
            CancellationToken.None);

        Assert.NotNull(copyId);
        var copy = await _places.GetByIdAsync(RecipientUserId, copyId!.Value, CancellationToken.None);
        Assert.Equal("The bakery they showed me", copy!.Name);
        // One place on the owner's map, still - the original and nothing else.
        Assert.Equal([place.Id], (await _places.GetAllAsync(OwnerUserId, null, CancellationToken.None))
            .Select(kept => kept.Id));
    }

    /// <summary>
    /// Getting rid of one they were handed takes it off their own map. Destroying a place somebody else
    /// keeps is not theirs to do, and the owner's is untouched.
    /// </summary>
    [Fact]
    public async Task Deleting_a_place_that_was_handed_over_only_takes_it_off_that_persons_map()
    {
        var place = await APlaceAsync();
        await AcceptAsync((await ShareAsync(place.Id))!.ShareId);
        var tombstones = new InMemorySyncTombstoneRepository();

        var gone = await new DeletePlaceCommandHandler(_places, _shares, tombstones).HandleAsync(
            new DeletePlaceCommand(RecipientUserId, place.Id), CancellationToken.None);

        Assert.True(gone);
        Assert.Empty(await new GetPlacesQueryHandler(Access).HandleAsync(
            new GetPlacesQuery(RecipientUserId), CancellationToken.None));
        Assert.NotNull(await _places.GetByIdAsync(OwnerUserId, place.Id, CancellationToken.None));
        // The tombstone is per-user, which is what lets a dropped grant leave one.
        Assert.Equal(RecipientUserId, Assert.Single(tombstones.Tombstones).UserId);
    }

    /// <summary>
    /// Sharing again is a reminder rather than a second row, and raising the level is how a request for
    /// edit access gets answered.
    /// </summary>
    [Fact]
    public async Task Sharing_the_same_place_twice_raises_the_offer_rather_than_making_another()
    {
        var place = await APlaceAsync();
        var first = await ShareAsync(place.Id);

        var second = await ShareAsync(place.Id, ShareAccessLevel.CanEdit);

        Assert.Equal(first!.ShareId, second!.ShareId);
        Assert.True(second.AlreadyShared);
        Assert.True(second.AccessLevelRaised);
    }

    /// <summary>Nobody may hand a place back to the person who keeps it - they already have all of it.</summary>
    [Fact]
    public async Task A_place_cannot_be_handed_back_to_the_person_who_keeps_it()
    {
        var place = await APlaceAsync();

        var outcome = await new SharePlaceCommandHandler(Access, _shares, _notifier).HandleAsync(
            new SharePlaceCommand(OwnerUserId, place.Id, OwnerUserId), CancellationToken.None);

        Assert.Null(outcome);
    }

    /// <summary>
    /// A reader given it to look at cannot pass on more than they were given - the cap every kind of
    /// share here shares, held by ShareAccess.CanGrant.
    /// </summary>
    [Fact]
    public async Task A_reader_cannot_hand_on_more_than_they_were_given()
    {
        var place = await APlaceAsync();
        await AcceptAsync((await ShareAsync(place.Id))!.ShareId);
        var somebodyElse = Guid.NewGuid();

        var outcome = await new SharePlaceCommandHandler(Access, _shares, _notifier).HandleAsync(
            new SharePlaceCommand(RecipientUserId, place.Id, somebodyElse, ShareAccessLevel.CanEdit),
            CancellationToken.None);

        Assert.Null(outcome);
    }

    /// <summary>An offer made to somebody else is not there to be read - the same answer a stale id gets.</summary>
    [Fact]
    public async Task An_offer_made_to_somebody_else_cannot_be_accepted()
    {
        var place = await APlaceAsync();
        var outcome = await ShareAsync(place.Id);

        var accepted = await new AcceptPlaceShareCommandHandler(_shares).HandleAsync(
            new AcceptPlaceShareCommand(Guid.NewGuid(), outcome!.ShareId), CancellationToken.None);

        Assert.False(accepted);
        Assert.Null(await new GetPlaceByIdQueryHandler(Access).HandleAsync(
            new GetPlaceByIdQuery(Guid.NewGuid(), place.Id), CancellationToken.None));
    }

    private static EventLocation Somewhere() => new("Piękna 1, Warszawa", 52.2297, 21.0122);
}
