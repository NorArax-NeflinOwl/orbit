using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Places;
using Orbit.Core.Places.DuplicatePlace;
using Orbit.Core.Places.SharePlace;
using Orbit.Core.Sharing;
using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Places;

/// <summary>
/// A place is sealed unless its owner says otherwise, which is the opposite default from every other
/// kind of thing in Orbit. What these hold in place is that one default and everything that follows from
/// it: the server keeps no readable copy, and everything that needs one is refused rather than half-done.
/// </summary>
public sealed class SealedPlaceTests
{
    private static readonly EncryptedPayload Sealed = new("c2VhbGVk", "bm9uY2U=");

    private static EventLocation Somewhere() => new("Piękna 1, Warszawa", 52.2297, 21.0122);

    /// <summary>
    /// The default itself. A caller that says nothing about privacy gets a sealed place, which is what
    /// stops a client that has not been taught about sealing making an open one by accident.
    /// </summary>
    [Fact]
    public void A_place_is_sealed_unless_it_is_told_not_to_be()
    {
        var place = Place.Create(Guid.NewGuid(), "The good bakery", "Sourdough", Somewhere(), encryptedContent: Sealed);

        Assert.True(place.IsPrivate);
        Assert.NotNull(place.EncryptedContent);
    }

    /// <summary>
    /// Sealed means the readable columns go empty rather than merely unread - the point with the words.
    /// A place whose coordinates were still readable would be sealed in name only, which is the opposite
    /// of what somebody sealing a place is asking for.
    /// </summary>
    [Fact]
    public void Sealing_empties_the_name_the_description_and_the_point()
    {
        var place = Place.Create(Guid.NewGuid(), "The good bakery", "Sourdough", Somewhere(), encryptedContent: Sealed);

        Assert.Equal(string.Empty, place.Name);
        Assert.Equal(string.Empty, place.Description);
        Assert.Equal(string.Empty, place.Where.Address);
        Assert.Equal(0, place.Where.Latitude);
        Assert.Equal(0, place.Where.Longitude);
    }

    /// <summary>What is left readable is what a map needs to draw nothing in particular.</summary>
    [Fact]
    public void Sealing_leaves_the_colour_the_priority_and_the_lists_alone()
    {
        var onTheList = Guid.NewGuid();

        var place = Place.Create(
            Guid.NewGuid(), "The good bakery", "", Somewhere(), "#cc4a3f", ItemPriority.High, [onTheList],
            encryptedContent: Sealed);

        Assert.Equal("#cc4a3f", place.Colour);
        Assert.Equal(ItemPriority.High, place.Priority);
        Assert.Equal([onTheList], place.TaskListIds);
    }

    /// <summary>A place said to be private with nothing sealed in it is a client that got it wrong.</summary>
    [Fact]
    public void A_private_place_arriving_with_nothing_sealed_is_refused()
        => Assert.Throws<InvalidRequestException>(
            () => Place.Create(Guid.NewGuid(), "The good bakery", "", Somewhere()));

    /// <summary>And an open one still needs a name, the way a note needs a title or a line.</summary>
    [Fact]
    public void An_open_place_still_needs_a_name()
        => Assert.Throws<InvalidRequestException>(
            () => Place.Create(Guid.NewGuid(), "  ", "", Somewhere(), isPrivate: false));

    /// <summary>Turning sealing off puts the words back in the columns, and drops the ciphertext.</summary>
    [Fact]
    public void Unsealing_a_place_makes_it_readable_again()
    {
        var place = Place.Create(Guid.NewGuid(), "The good bakery", "Sourdough", Somewhere(), encryptedContent: Sealed);

        place.Update(
            "The good bakery", "Sourdough", Somewhere(), "", ItemPriority.Normal, null, isPrivate: false);

        Assert.False(place.IsPrivate);
        Assert.Null(place.EncryptedContent);
        Assert.Equal("The good bakery", place.Name);
        Assert.Equal(52.2297, place.Where.Latitude);
    }

    /// <summary>
    /// A sealed place is offered to nobody, exactly as a private note is: the server holds no readable
    /// copy to hand over, which is what makes it sealed. It matters more here than anywhere else,
    /// because the ordinary case is the refused one.
    /// </summary>
    [Fact]
    public async Task A_sealed_place_cannot_be_shared()
    {
        var context = new SealedPlaceContext();
        var place = await context.AddSealedPlaceAsync();

        await Assert.ThrowsAsync<InvalidRequestException>(() => context.ShareAsync(place.Id));
    }

    /// <summary>
    /// Nor copied here: sealing is the client's work and the server has no key, so a copy it made would
    /// be an empty place wearing the name of a full one.
    /// </summary>
    [Fact]
    public async Task A_sealed_place_cannot_be_copied_by_the_server()
    {
        var context = new SealedPlaceContext();
        var place = await context.AddSealedPlaceAsync();

        await Assert.ThrowsAsync<InvalidRequestException>(() => context.DuplicateAsync(place.Id));
    }

    /// <summary>
    /// And no public link may be made for one - the same rule a private note follows. Sealing a place
    /// that already had a link closes the link with it rather than merely stopping new ones.
    /// </summary>
    [Fact]
    public async Task A_sealed_place_cannot_be_published()
    {
        var context = new SealedPlaceContext();
        var place = await context.AddSealedPlaceAsync();

        Assert.False(await context.CanPublishAsync(place.Id));
    }

    [Fact]
    public async Task An_open_place_can_be_published()
    {
        var context = new SealedPlaceContext();
        var place = await context.AddOpenPlaceAsync();

        Assert.True(await context.CanPublishAsync(place.Id));
    }

    /// <summary>Everything a sealed-place test needs, and nothing that is not about sealing.</summary>
    private sealed class SealedPlaceContext
    {
        private readonly InMemoryPlaceRepository _places = new();
        private readonly InMemoryPlaceShareRepository _shares = new();
        private readonly InMemoryUserRepository _users = new();
        private readonly RecordingSharedItemNotifier _notifier = new();

        public Guid OwnerUserId { get; } = Guid.NewGuid();

        private PlaceAccessResolver Access => new(_places, _shares, _users);

        public async Task<Place> AddSealedPlaceAsync()
        {
            var place = Place.Create(OwnerUserId, "The good bakery", "", Somewhere(), encryptedContent: Sealed);
            await _places.AddAsync(place, CancellationToken.None);
            return place;
        }

        public async Task<Place> AddOpenPlaceAsync()
        {
            var place = Place.Create(OwnerUserId, "The good bakery", "", Somewhere(), isPrivate: false);
            await _places.AddAsync(place, CancellationToken.None);
            return place;
        }

        public Task ShareAsync(Guid placeId)
            => new SharePlaceCommandHandler(Access, _shares, _notifier).HandleAsync(
                new SharePlaceCommand(OwnerUserId, placeId, Guid.NewGuid()), CancellationToken.None);

        public Task DuplicateAsync(Guid placeId)
            => new DuplicatePlaceCommandHandler(Access, _places).HandleAsync(
                new DuplicatePlaceCommand(OwnerUserId, placeId), CancellationToken.None);

        public Task<bool> CanPublishAsync(Guid placeId)
            => new PublicSharedItemReader(
                    new InMemoryNoteRepository(), new InMemoryTaskRepository(),
                    new InMemoryCalendarEventRepository(), new InMemoryInventoryRepository(),
                    new InMemoryInventoryItemRepository(), _places, _users)
                .CanPublishAsync(OwnerUserId, SharedItemType.Place, placeId, CancellationToken.None);
    }
}
